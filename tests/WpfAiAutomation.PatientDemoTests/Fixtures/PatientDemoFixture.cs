using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;
using WpfAiAutomation.FlaUI;
using WpfAiAutomation.PatientDemoTests.PageObjects;

namespace WpfAiAutomation.PatientDemoTests.Fixtures;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "xUnit owns the IAsyncLifetime lifecycle and invokes asynchronous cleanup.")]
public sealed class PatientDemoFixture : IAsyncLifetime
{
    private AutomationConfiguration? _configuration;
    private AutomationSessionManager? _sessionManager;
    private RedactedScreenshotService? _screenshots;
    private string? _sessionId;
    private AutomationSessionInfo? _sessionInfo;
    private long _startupDurationMilliseconds;
    private string? _processLeasePath;

    public PatientSearchPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _configuration = PatientDemoTestEnvironment.LoadConfiguration();
        var redactor = new SensitiveDataRedactor(_configuration.SensitiveControls);
        var inspector = new UiInspector(redactor, _configuration.Policy);
        _sessionManager = new AutomationSessionManager(new ApplicationCatalog(_configuration), inspector);
        _screenshots = new RedactedScreenshotService(_configuration.Evidence, redactor);

        var startup = Stopwatch.StartNew();
        var launch = await _sessionManager.LaunchAsync(new ApplicationLaunchRequest("patient-demo")).ConfigureAwait(false);
        _startupDurationMilliseconds = startup.ElapsedMilliseconds;
        if (!launch.Succeeded || launch.Value is null)
        {
            throw new InvalidOperationException(launch.Message ?? "Patient Demo could not be launched.");
        }

        _sessionId = launch.Value.SessionId;
        _sessionInfo = launch.Value;
        Page = new PatientSearchPage(_sessionManager, new UiActionService(inspector), new UiStateService(inspector), _sessionId);
        _processLeasePath = await CreateProcessLeaseAsync(launch.Value).ConfigureAwait(false);
    }

    public async Task RunWithFailureEvidenceAsync(string testId, Func<PatientSearchPage, Task> test)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testId);
        ArgumentNullException.ThrowIfNull(test);
        EnsureInitialized();

        var runId = $"regression-{testId}-{Guid.NewGuid():N}";
        await using var recorder = await EvidenceRecorder.CreateAsync(_configuration!.Evidence, runId, testId).ConfigureAwait(false);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var operations = new List<PatientSearchOperation>();
        Page.OperationObserver = operations.Add;

        try
        {
            await test(Page).ConfigureAwait(false);
            await RecordOperationsAsync(recorder, runId, testId, operations).ConfigureAwait(false);
            await WriteRunMetadataAsync(recorder.DirectoryPath, runId, testId, stopwatch.ElapsedMilliseconds).ConfigureAwait(false);
            await recorder.AppendAsync(CreateEvidenceEvent(
                runId, testId, operations.Count + 1, startedAtUtc, stopwatch.ElapsedMilliseconds, EvidenceResult.Passed, null, null, operations.LastOrDefault())).ConfigureAwait(false);
        }
        catch
        {
            await RecordOperationsAsync(recorder, runId, testId, operations).ConfigureAwait(false);
            await WriteRunMetadataAsync(recorder.DirectoryPath, runId, testId, stopwatch.ElapsedMilliseconds).ConfigureAwait(false);
            var artifacts = await CaptureFailureScreenshotAsync(runId, testId).ConfigureAwait(false);
            await recorder.AppendAsync(CreateEvidenceEvent(
                runId,
                testId,
                operations.Count + 1,
                startedAtUtc,
                stopwatch.ElapsedMilliseconds,
                EvidenceResult.Failed,
                ToolErrorCode.AssertionFailed,
                artifacts,
                operations.LastOrDefault())).ConfigureAwait(false);
            throw;
        }
        finally
        {
            Page.OperationObserver = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_sessionManager is not null)
        {
            await _sessionManager.DisposeAsync().ConfigureAwait(false);
        }

        if (_processLeasePath is not null && IsProcessExited(_sessionInfo?.ProcessId))
        {
            File.Delete(_processLeasePath);
        }
    }

    private async Task<IReadOnlyList<EvidenceReference>?> CaptureFailureScreenshotAsync(string runId, string testId)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var result = await _screenshots!.CaptureAsync(
            _sessionManager!,
            _sessionId!,
            new AutomationOperationContext(runId, testId, 1, correlationId)).ConfigureAwait(false);

        return result.Succeeded && result.Value is not null ? [result.Value] : null;
    }

    private static EvidenceEvent CreateEvidenceEvent(
        string runId,
        string testId,
        int stepNumber,
        DateTimeOffset startedAtUtc,
        long durationMilliseconds,
        EvidenceResult result,
        ToolErrorCode? errorCode,
        IReadOnlyList<EvidenceReference>? artifacts,
        PatientSearchOperation? lastOperation) => new(
            runId,
            testId,
            stepNumber,
            AutomationAction.Assert,
            lastOperation?.ResolvedElement,
            lastOperation?.Before,
            lastOperation?.After,
            result,
            errorCode,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            durationMilliseconds,
            artifacts,
            Guid.NewGuid().ToString("N"));

    private static async Task RecordOperationsAsync(
        EvidenceRecorder recorder,
        string runId,
        string testId,
        IReadOnlyList<PatientSearchOperation> operations)
    {
        var stepNumber = 1;
        foreach (var operation in operations)
        {
            var completedAtUtc = DateTimeOffset.UtcNow;
            await recorder.AppendAsync(new EvidenceEvent(
                runId,
                testId,
                stepNumber++,
                operation.Action,
                operation.ResolvedElement,
                operation.Before,
                operation.After,
                operation.Succeeded ? EvidenceResult.Passed : EvidenceResult.Failed,
                operation.ErrorCode,
                completedAtUtc - TimeSpan.FromMilliseconds(operation.DurationMilliseconds),
                completedAtUtc,
                operation.DurationMilliseconds,
                CorrelationId: operation.CorrelationId)).ConfigureAwait(false);
        }
    }

    private async Task WriteRunMetadataAsync(string directory, string runId, string testId, long scenarioDurationMilliseconds)
    {
        var metadata = new RegressionRunMetadata(
            runId,
            testId,
            _sessionInfo!.ApplicationVersion,
            _sessionInfo.ProcessId,
            _startupDurationMilliseconds,
            scenarioDurationMilliseconds);
        await File.WriteAllTextAsync(Path.Combine(directory, "run-metadata.json"), ContractsJson.Serialize(metadata)).ConfigureAwait(false);
    }

    private async Task<string> CreateProcessLeaseAsync(AutomationSessionInfo session)
    {
        var directory = Environment.GetEnvironmentVariable("WPF_AI_AUTOMATION_PROCESS_LEASE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(_configuration!.Evidence.RootDirectory, "process-leases");
        }

        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var runId = $"ci-{Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "local"}-{Guid.NewGuid():N}";
        var lease = new PatientDemoProcessLease(
            runId,
            session.ProcessId,
            _configuration!.Applications["patient-demo"].ProcessName,
            _configuration.Applications["patient-demo"].ExecutablePath,
            DateTimeOffset.UtcNow);
        var path = Path.Combine(directory, $"{session.SessionId}.json");
        await File.WriteAllTextAsync(path, ContractsJson.Serialize(lease)).ConfigureAwait(false);
        return path;
    }

    private static bool IsProcessExited(int? processId)
    {
        if (processId is null)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private void EnsureInitialized()
    {
        if (_configuration is null || _sessionManager is null || _screenshots is null || _sessionId is null)
        {
            throw new InvalidOperationException("The Patient Demo fixture has not been initialized.");
        }
    }
}

internal sealed record RegressionRunMetadata(
    string RunId,
    string TestId,
    string? ApplicationVersion,
    int ProcessId,
    long StartupDurationMilliseconds,
    long ScenarioDurationMilliseconds);

internal sealed record PatientDemoProcessLease(
    string RunId,
    int ProcessId,
    string ProcessName,
    string ExecutablePath,
    DateTimeOffset CreatedAtUtc);
