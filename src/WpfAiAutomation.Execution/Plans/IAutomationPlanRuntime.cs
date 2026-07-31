using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.Execution;

public interface IAutomationPlanRuntime
{
    Task<ToolResult<AutomationSessionInfo>> LaunchAsync(ApplicationLaunchRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<bool>> CloseAsync(CancellationToken cancellationToken = default);

    Task<ToolResult<ActionExecutionResult>> SetTextAsync(SetTextRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<ActionExecutionResult>> InvokeAsync(InvokeRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<ActionExecutionResult>> SelectItemAsync(SelectItemRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<ObservationResult>> ReadAsync(ReadElementRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<ObservationResult>> WaitAsync(WaitForRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<AssertionResult>> AssertAsync(AssertionRequest request, CancellationToken cancellationToken = default);

    Task<ToolResult<EvidenceReference>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken = default);
}
