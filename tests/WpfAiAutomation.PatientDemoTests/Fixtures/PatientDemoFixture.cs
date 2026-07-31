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

    public PatientSearchPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _configuration = PatientDemoTestEnvironment.LoadConfiguration();
        var redactor = new SensitiveDataRedactor(_configuration.SensitiveControls);
        var inspector = new UiInspector(redactor, _configuration.Policy);
        _sessionManager = new AutomationSessionManager(new ApplicationCatalog(_configuration), inspector);
        _screenshots = new RedactedScreenshotService(_configuration.Evidence, redactor);

        var launch = await _sessionManager.LaunchAsync(new ApplicationLaunchRequest("patient-demo")).ConfigureAwait(false);
        if (!launch.Succeeded || launch.Value is null)
        {
            throw new InvalidOperationException(launch.Message ?? "Patient Demo could not be launched.");
        }

        _sessionId = launch.Value.SessionId;
        Page = new PatientSearchPage(_sessionManager, new UiActionService(inspector), new UiStateService(inspector), _sessionId);
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

        try
        {
            await test(Page).ConfigureAwait(false);
            await recorder.AppendAsync(CreateEvidenceEvent(
                runId, testId, startedAtUtc, stopwatch.ElapsedMilliseconds, EvidenceResult.Passed, null, null)).ConfigureAwait(false);
        }
        catch
        {
            var artifacts = await CaptureFailureScreenshotAsync(runId, testId).ConfigureAwait(false);
            await recorder.AppendAsync(CreateEvidenceEvent(
                runId,
                testId,
                startedAtUtc,
                stopwatch.ElapsedMilliseconds,
                EvidenceResult.Failed,
                ToolErrorCode.AssertionFailed,
                artifacts)).ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_sessionManager is not null)
        {
            await _sessionManager.DisposeAsync().ConfigureAwait(false);
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
        DateTimeOffset startedAtUtc,
        long durationMilliseconds,
        EvidenceResult result,
        ToolErrorCode? errorCode,
        IReadOnlyList<EvidenceReference>? artifacts) => new(
            runId,
            testId,
            1,
            AutomationAction.Assert,
            null,
            null,
            null,
            result,
            errorCode,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            durationMilliseconds,
            artifacts,
            Guid.NewGuid().ToString("N"));

    private void EnsureInitialized()
    {
        if (_configuration is null || _sessionManager is null || _screenshots is null || _sessionId is null)
        {
            throw new InvalidOperationException("The Patient Demo fixture has not been initialized.");
        }
    }
}
