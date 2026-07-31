using System.Text;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.Execution;

public sealed class EvidenceRecorder : IAsyncDisposable
{
    private readonly string _runId;
    private readonly string _testId;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly StreamWriter _eventWriter;
    private int _passedSteps;
    private int _failedSteps;
    private int _skippedSteps;
    private bool _finalized;

    private EvidenceRecorder(
        string runId,
        string testId,
        string directory,
        StreamWriter eventWriter)
    {
        _runId = runId;
        _testId = testId;
        DirectoryPath = directory;
        _eventWriter = eventWriter;
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    public string DirectoryPath { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public static async Task<EvidenceRecorder> CreateAsync(
        EvidencePathConfiguration configuration,
        string runId,
        string testId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateRunContext(runId, testId);

        var directory = Path.Combine(Path.GetFullPath(configuration.RootDirectory), runId);
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "screenshots"));
        Directory.CreateDirectory(Path.Combine(directory, "ui-trees"));

        var eventPath = Path.Combine(directory, "events.jsonl");
        var stream = new FileStream(eventPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return new EvidenceRecorder(runId, testId, directory, writer);
    }

    public async Task AppendAsync(EvidenceEvent evidenceEvent, CancellationToken cancellationToken = default)
    {
        if (_finalized)
        {
            throw new InvalidOperationException("Evidence has already been finalized.");
        }

        ArgumentNullException.ThrowIfNull(evidenceEvent);
        ValidateEventContext(evidenceEvent);

        var validation = ContractValidators.Validate(evidenceEvent);
        if (!validation.IsValid)
        {
            throw new ArgumentException("The evidence event is invalid.", nameof(evidenceEvent));
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _eventWriter.WriteLineAsync(ContractsJson.SerializeCompact(evidenceEvent).AsMemory(), cancellationToken).ConfigureAwait(false);
            await _eventWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            Count(evidenceEvent.Result);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<EvidenceRunSummary> FinalizeAsync(CancellationToken cancellationToken = default)
    {
        if (_finalized)
        {
            throw new InvalidOperationException("Evidence has already been finalized.");
        }

        _finalized = true;
        var completedAtUtc = DateTimeOffset.UtcNow;
        var summary = new EvidenceRunSummary(
            _runId,
            _testId,
            StartedAtUtc,
            completedAtUtc,
            _passedSteps,
            _failedSteps,
            _skippedSteps,
            _runId);

        var summaryPath = Path.Combine(DirectoryPath, "run.json");
        await File.WriteAllTextAsync(summaryPath, ContractsJson.Serialize(summary), cancellationToken).ConfigureAwait(false);
        await _eventWriter.DisposeAsync().ConfigureAwait(false);
        return summary;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_finalized)
        {
            await FinalizeAsync().ConfigureAwait(false);
        }

        _writeLock.Dispose();
    }

    private void ValidateEventContext(EvidenceEvent evidenceEvent)
    {
        if (!string.Equals(evidenceEvent.RunId, _runId, StringComparison.Ordinal)
            || !string.Equals(evidenceEvent.TestId, _testId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Evidence event identifiers do not match the active run.", nameof(evidenceEvent));
        }
    }

    private static void ValidateRunContext(string? runId, string? testId)
    {
        if (!IsSafeIdentifier(runId) || !IsSafeIdentifier(testId))
        {
            throw new ArgumentException("Evidence run identifiers are invalid.");
        }
    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private void Count(EvidenceResult result)
    {
        switch (result)
        {
            case EvidenceResult.Passed:
                _passedSteps++;
                break;
            case EvidenceResult.Failed:
                _failedSteps++;
                break;
            case EvidenceResult.Skipped:
                _skippedSteps++;
                break;
        }
    }
}
