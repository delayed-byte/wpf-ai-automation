using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;
using WpfAiAutomation.FlaUI;

namespace AgentServer;

public sealed class FlaUiPlanRuntime : IAutomationPlanRuntime
{
    private readonly AutomationSessionManager _sessions;
    private readonly UiActionService _actions;
    private readonly UiStateService _state;
    private readonly RedactedScreenshotService _screenshots;

    public FlaUiPlanRuntime(
        AutomationSessionManager sessions,
        UiActionService actions,
        UiStateService state,
        RedactedScreenshotService screenshots)
    {
        _sessions = sessions;
        _actions = actions;
        _state = state;
        _screenshots = screenshots;
    }

    public Task<ToolResult<AutomationSessionInfo>> LaunchAsync(ApplicationLaunchRequest request, CancellationToken cancellationToken = default) =>
        _sessions.LaunchAsync(request, cancellationToken);

    public Task<ToolResult<bool>> CloseAsync(CancellationToken cancellationToken = default) =>
        _sessions.CloseAsync(cancellationToken);

    public Task<ToolResult<ActionExecutionResult>> SetTextAsync(SetTextRequest request, CancellationToken cancellationToken = default) =>
        _actions.SetTextAsync(_sessions, request, cancellationToken);

    public Task<ToolResult<ActionExecutionResult>> InvokeAsync(InvokeRequest request, CancellationToken cancellationToken = default) =>
        _actions.InvokeAsync(_sessions, request, cancellationToken);

    public Task<ToolResult<ActionExecutionResult>> SelectItemAsync(SelectItemRequest request, CancellationToken cancellationToken = default) =>
        _actions.SelectItemAsync(_sessions, request, cancellationToken);

    public Task<ToolResult<ObservationResult>> ReadAsync(ReadElementRequest request, CancellationToken cancellationToken = default) =>
        _state.ReadAsync(_sessions, request, cancellationToken);

    public Task<ToolResult<ObservationResult>> WaitAsync(WaitForRequest request, CancellationToken cancellationToken = default) =>
        _state.WaitAsync(_sessions, request.SessionId, request.Target, request.Condition, cancellationToken);

    public Task<ToolResult<AssertionResult>> AssertAsync(AssertionRequest request, CancellationToken cancellationToken = default) =>
        _state.AssertAsync(_sessions, request, cancellationToken);

    public Task<ToolResult<EvidenceReference>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken = default) =>
        _screenshots.CaptureAsync(
            _sessions,
            request.SessionId,
            new AutomationOperationContext(request.RunId, request.TestId, request.StepNumber, request.CorrelationId),
            cancellationToken);
}
