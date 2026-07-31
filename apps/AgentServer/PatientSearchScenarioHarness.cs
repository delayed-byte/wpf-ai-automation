using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;
using WpfAiAutomation.FlaUI;

namespace AgentServer;

public sealed class PatientSearchScenarioHarness : IAsyncDisposable
{
    private const string TestId = "patient-search";
    private readonly AutomationConfiguration _configuration;
    private readonly AutomationSessionManager _sessionManager;
    private readonly UiActionService _actions;
    private readonly UiStateService _state;
    private readonly RedactedScreenshotService _screenshots;

    public PatientSearchScenarioHarness(AutomationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        var redactor = new SensitiveDataRedactor(configuration.SensitiveControls);
        var inspector = new UiInspector(redactor, configuration.Policy);
        _sessionManager = new AutomationSessionManager(new ApplicationCatalog(configuration), inspector);
        _actions = new UiActionService(inspector);
        _state = new UiStateService(inspector);
        _screenshots = new RedactedScreenshotService(configuration.Evidence, redactor);
    }

    public async Task<ToolResult<EvidenceRunSummary>> RunAsync(bool deliberatelyUseWrongExpectedName, CancellationToken cancellationToken = default)
    {
        var runId = $"patient-search-{Guid.NewGuid():N}";
        var started = DateTimeOffset.UtcNow;
        var recorder = await EvidenceRecorder.CreateAsync(_configuration.Evidence, runId, TestId, cancellationToken).ConfigureAwait(false);
        EvidenceRunSummary? summary = null;

        try
        {
            var launch = await _sessionManager.LaunchAsync(new ApplicationLaunchRequest("patient-demo"), cancellationToken).ConfigureAwait(false);
            if (!launch.Succeeded || launch.Value is null)
            {
                return Failure(launch.ErrorCode ?? ToolErrorCode.ApplicationLaunchFailed, launch.Message ?? "Patient Demo could not be launched.", started);
            }

            var sessionId = launch.Value.SessionId;
            var setText = await _actions.SetTextAsync(
                _sessionManager,
                new SetTextRequest(sessionId, new ElementLocator("PatientIdTextBox", AutomationControlType.Edit), "PM-1001"),
                cancellationToken).ConfigureAwait(false);
            await RecordActionAsync(recorder, runId, 1, AutomationAction.SetText, setText, cancellationToken).ConfigureAwait(false);
            if (!setText.Succeeded)
            {
                return Failure(setText.ErrorCode ?? ToolErrorCode.AutomationFailure, setText.Message ?? "Patient ID entry failed.", started);
            }

            var invoke = await _actions.InvokeAsync(
                _sessionManager,
                new InvokeRequest(sessionId, new ElementLocator("SearchButton", AutomationControlType.Button)),
                cancellationToken).ConfigureAwait(false);
            await RecordActionAsync(recorder, runId, 2, AutomationAction.Invoke, invoke, cancellationToken).ConfigureAwait(false);
            if (!invoke.Succeeded)
            {
                return Failure(invoke.ErrorCode ?? ToolErrorCode.AutomationFailure, invoke.Message ?? "Search invocation failed.", started);
            }

            var statusTarget = new ElementLocator("StatusMessage", AutomationControlType.Text);
            var wait = await _state.WaitAsync(
                _sessionManager,
                sessionId,
                statusTarget,
                new WaitCondition(AutomationProperty.Name, ComparisonOperator.Equals, "1 patient found."),
                cancellationToken).ConfigureAwait(false);
            await RecordObservationAsync(recorder, runId, 3, AutomationAction.WaitFor, wait, cancellationToken).ConfigureAwait(false);
            if (!wait.Succeeded)
            {
                return Failure(wait.ErrorCode ?? ToolErrorCode.ConditionTimeout, wait.Message ?? "Search result did not become ready.", started);
            }

            var expectedName = deliberatelyUseWrongExpectedName ? "Avery Chen" : "1 patient found.";
            var assertion = await _state.AssertAsync(
                _sessionManager,
                new AssertionRequest(sessionId, statusTarget, AutomationProperty.Name, ComparisonOperator.Equals, expectedName),
                cancellationToken).ConfigureAwait(false);
            await RecordAssertionAsync(recorder, runId, 4, assertion, sessionId, cancellationToken).ConfigureAwait(false);

            if (!assertion.Succeeded)
            {
                return Failure(assertion.ErrorCode ?? ToolErrorCode.AssertionFailed, assertion.Message ?? "The expected patient name did not match.", started);
            }

            summary = await recorder.FinalizeAsync(cancellationToken).ConfigureAwait(false);
            return Success(summary, started);
        }
        finally
        {
            await _sessionManager.CloseAsync(cancellationToken).ConfigureAwait(false);
            if (summary is null)
            {
                await recorder.FinalizeAsync(cancellationToken).ConfigureAwait(false);
            }

            await recorder.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await _sessionManager.DisposeAsync().ConfigureAwait(false);

    private static async Task RecordActionAsync(
        EvidenceRecorder recorder,
        string runId,
        int stepNumber,
        AutomationAction action,
        ToolResult<ActionExecutionResult> result,
        CancellationToken cancellationToken)
    {
        var completed = DateTimeOffset.UtcNow;
        await recorder.AppendAsync(new EvidenceEvent(
            runId,
            TestId,
            stepNumber,
            action,
            result.Value?.After,
            result.Value?.Before,
            result.Value?.After,
            result.Succeeded ? EvidenceResult.Passed : EvidenceResult.Failed,
            result.ErrorCode,
            completed - TimeSpan.FromMilliseconds(result.DurationMilliseconds),
            completed,
            result.DurationMilliseconds,
            CorrelationId: result.CorrelationId), cancellationToken).ConfigureAwait(false);
    }

    private static async Task RecordObservationAsync(
        EvidenceRecorder recorder,
        string runId,
        int stepNumber,
        AutomationAction action,
        ToolResult<ObservationResult> result,
        CancellationToken cancellationToken)
    {
        var completed = DateTimeOffset.UtcNow;
        await recorder.AppendAsync(new EvidenceEvent(
            runId,
            TestId,
            stepNumber,
            action,
            result.Value?.Element,
            result.Value?.Element,
            result.Value?.Element,
            result.Succeeded ? EvidenceResult.Passed : EvidenceResult.Failed,
            result.ErrorCode,
            completed - TimeSpan.FromMilliseconds(result.DurationMilliseconds),
            completed,
            result.DurationMilliseconds,
            CorrelationId: result.CorrelationId), cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordAssertionAsync(
        EvidenceRecorder recorder,
        string runId,
        int stepNumber,
        ToolResult<AssertionResult> result,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var context = new AutomationOperationContext(runId, TestId, stepNumber, result.CorrelationId);
        var artifacts = new List<EvidenceReference>();
        if (!result.Succeeded && _configuration.Evidence.CaptureScreenshotOnFailure)
        {
            var screenshot = await _screenshots.CaptureAsync(_sessionManager, sessionId, context, cancellationToken).ConfigureAwait(false);
            if (screenshot.Succeeded && screenshot.Value is not null)
            {
                artifacts.Add(screenshot.Value);
            }
        }

        var completed = DateTimeOffset.UtcNow;
        await recorder.AppendAsync(new EvidenceEvent(
            runId,
            TestId,
            stepNumber,
            AutomationAction.Assert,
            result.Value?.Element,
            result.Value?.Element,
            result.Value?.Element,
            result.Succeeded ? EvidenceResult.Passed : EvidenceResult.Failed,
            result.ErrorCode,
            completed - TimeSpan.FromMilliseconds(result.DurationMilliseconds),
            completed,
            result.DurationMilliseconds,
            artifacts,
            result.CorrelationId), cancellationToken).ConfigureAwait(false);
    }

    private static ToolResult<EvidenceRunSummary> Success(EvidenceRunSummary summary, DateTimeOffset started) => new(
        true, summary, null, null, Duration(started), Guid.NewGuid().ToString("N"));

    private static ToolResult<EvidenceRunSummary> Failure(ToolErrorCode errorCode, string message, DateTimeOffset started) => new(
        false, null, errorCode, message, Duration(started), Guid.NewGuid().ToString("N"));

    private static long Duration(DateTimeOffset started) => (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
}
