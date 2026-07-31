using System.ComponentModel;
using ModelContextProtocol.Server;
using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;
using WpfAiAutomation.FlaUI;

namespace AgentServer;

[McpServerToolType]
public sealed class AutomationTools
{
    private readonly AutomationSessionManager _sessions;
    private readonly UiInspector _inspector;
    private readonly IAutomationPlanRuntime _runtime;
    private readonly TestPlanRunner _runner;
    private readonly TestPlanValidator _validator;
    private readonly PolicyCeilings _ceilings;

    public AutomationTools(
        AutomationSessionManager sessions,
        UiInspector inspector,
        IAutomationPlanRuntime runtime,
        TestPlanRunner runner,
        TestPlanValidator validator,
        PolicyCeilings ceilings)
    {
        _sessions = sessions;
        _inspector = inspector;
        _runtime = runtime;
        _runner = runner;
        _validator = validator;
        _ceilings = ceilings;
    }

    [McpServerTool(Name = "launch_application", Destructive = false, Idempotent = false)]
    [Description("Launches one configured allowlisted application. Fails if a session is active or the key or arguments are not approved.")]
    public Task<ToolResult<AutomationSessionInfo>> LaunchApplicationAsync(
        ApplicationLaunchRequest request,
        CancellationToken cancellationToken = default) =>
        _runtime.LaunchAsync(request, cancellationToken);

    [McpServerTool(Name = "close_application", Destructive = false, Idempotent = true)]
    [Description("Closes the active allowlisted application session using the configured bounded cleanup policy.")]
    public Task<ToolResult<bool>> CloseApplicationAsync(CancellationToken cancellationToken = default) =>
        _runtime.CloseAsync(cancellationToken);

    [McpServerTool(Name = "inspect_ui", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Returns a bounded redacted UI tree for the active session. Requires the opaque session ID.")]
    public Task<ToolResult<ElementSnapshot>> InspectUiAsync(
        UiTreeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId)
            || request.MaximumDepth is < 0
            || request.MaximumDepth > _ceilings.MaximumTreeDepth
            || request.MaximumElementCount is < 1
            || request.MaximumElementCount > _ceilings.MaximumTreeElementCount)
        {
            return Invalid<ElementSnapshot>("The session identifier or UI tree resource limits are invalid.");
        }

        return request.Root is null
            ? _inspector.InspectAsync(_sessions, request, cancellationToken)
            : InvalidLocator<ElementSnapshot>(request.Root) ?? _inspector.InspectAsync(_sessions, request, cancellationToken);
    }

    [McpServerTool(Name = "find_element", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Finds one element by exact AutomationId. Returns distinct not-found and ambiguous errors.")]
    public Task<ToolResult<ElementSnapshot>> FindElementAsync(
        FindElementRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidLocator<ElementSnapshot>(request.Target) ?? _inspector.FindAsync(_sessions, request, cancellationToken);

    [McpServerTool(Name = "read_element", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Reads one allowlisted property from an exactly located element in the active session.")]
    public Task<ToolResult<ObservationResult>> ReadElementAsync(
        ReadElementRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<ObservationResult>(new ReadElementStep(1, request.Target, request.Property, request.Target.TimeoutMilliseconds))
        ?? _runtime.ReadAsync(request, cancellationToken);

    [McpServerTool(Name = "set_text", Destructive = false, Idempotent = true)]
    [Description("Sets and verifies text on an enabled editable element identified by exact AutomationId.")]
    public Task<ToolResult<ActionExecutionResult>> SetTextAsync(
        SetTextRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<ActionExecutionResult>(new SetTextStep(1, request.Target, request.Value, request.Target.TimeoutMilliseconds))
        ?? _runtime.SetTextAsync(request, cancellationToken);

    [McpServerTool(Name = "invoke", Destructive = false, Idempotent = false)]
    [Description("Invokes an enabled element that supports the UI Automation Invoke pattern.")]
    public Task<ToolResult<ActionExecutionResult>> InvokeAsync(
        InvokeRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<ActionExecutionResult>(new InvokeStep(1, request.Target, request.Target.TimeoutMilliseconds))
        ?? _runtime.InvokeAsync(request, cancellationToken);

    [McpServerTool(Name = "select_item", Destructive = false, Idempotent = true)]
    [Description("Selects one exact AutomationId item within an enabled selection container.")]
    public Task<ToolResult<ActionExecutionResult>> SelectItemAsync(
        SelectItemRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<ActionExecutionResult>(new SelectItemStep(1, request.Target, request.ItemAutomationId, request.Target.TimeoutMilliseconds))
        ?? _runtime.SelectItemAsync(request, cancellationToken);

    [McpServerTool(Name = "wait_for", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Polls a supported UI condition with bounded intervals and timeout, resolving the element on every poll.")]
    public Task<ToolResult<ObservationResult>> WaitForAsync(
        WaitForRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<ObservationResult>(new WaitForStep(1, request.Target, request.Condition))
        ?? _runtime.WaitAsync(request, cancellationToken);

    [McpServerTool(Name = "assert_state", Destructive = false, ReadOnly = true, Idempotent = true)]
    [Description("Evaluates one supported property/operator assertion and returns expected and actual state on failure.")]
    public Task<ToolResult<AssertionResult>> AssertStateAsync(
        AssertionRequest request,
        CancellationToken cancellationToken = default) =>
        InvalidStep<AssertionResult>(new AssertStep(
            1, request.Target, request.Property, request.Operator, request.ExpectedValue, request.TimeoutMilliseconds))
        ?? _runtime.AssertAsync(request, cancellationToken);

    [McpServerTool(Name = "capture_screenshot", Destructive = false, ReadOnly = true, Idempotent = false)]
    [Description("Captures a redacted screenshot into the current run using safe run, test, step, and correlation identifiers.")]
    public Task<ToolResult<EvidenceReference>> CaptureScreenshotAsync(
        CaptureScreenshotRequest request,
        CancellationToken cancellationToken = default) =>
        !IsSafeIdentifier(request.SessionId)
            || !IsSafeIdentifier(request.RunId)
            || !IsSafeIdentifier(request.TestId)
            || !IsSafeIdentifier(request.CorrelationId)
            || request.StepNumber < 1
                ? Invalid<EvidenceReference>("Screenshot operation identifiers are invalid.")
                : _runtime.CaptureScreenshotAsync(request, cancellationToken);

    [McpServerTool(Name = "execute_plan", Destructive = false, Idempotent = false)]
    [Description("Validates the complete plan before launch, then executes steps sequentially with fail-fast cleanup and evidence.")]
    public Task<ToolResult<PlanExecutionSummary>> ExecutePlanAsync(
        TestPlan plan,
        CancellationToken cancellationToken = default) =>
        _runner.ExecuteAsync(plan, cancellationToken);

    private static Task<ToolResult<T>>? InvalidLocator<T>(ElementLocator locator)
    {
        var validation = ContractValidators.Validate(locator);
        if (validation.IsValid)
        {
            return null;
        }

        var message = string.Join(" ", validation.Errors.Select(error => $"{error.Field}: {error.Message}"));
        return Invalid<T>(message);
    }

    private Task<ToolResult<T>>? InvalidStep<T>(TestStep step)
    {
        var validation = _validator.ValidateStep(step);
        return validation.IsValid
            ? null
            : Invalid<T>(string.Join(" ", validation.Errors.Select(error => $"{error.Field}: {error.Message}")));
    }

    private static Task<ToolResult<T>> Invalid<T>(string message) => Task.FromResult(new ToolResult<T>(
        false, default, ToolErrorCode.InvalidRequest, message, 0, Guid.NewGuid().ToString("N")));

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
}
