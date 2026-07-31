using FlaUI.Core.AutomationElements;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed class UiActionService
{
    private readonly UiInspector _inspector;
    private readonly DeterministicAutomationFaultInjector _faultInjector;

    public UiActionService(UiInspector inspector, DeterministicAutomationFaultInjector? faultInjector = null)
    {
        _inspector = inspector;
        _faultInjector = faultInjector ?? new DeterministicAutomationFaultInjector();
    }

    public Task<ToolResult<ActionExecutionResult>> SetTextAsync(
        AutomationSessionManager sessionManager,
        SetTextRequest request,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            VerifySession(session, request.SessionId);
            var element = ResolveRequired(session, request.Target);
            var before = _inspector.Snapshot(element, includeChildren: false);

            if (!element.IsEnabled)
            {
                throw new AutomationOperationException(ToolErrorCode.ElementDisabled, "The target element is disabled.");
            }

            if (!element.Patterns.Value.IsSupported || element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault)
            {
                throw new AutomationOperationException(ToolErrorCode.UnsupportedPattern, "The target element does not support editable values.");
            }

            element.Patterns.Value.Pattern.SetValue(request.Value);
            var actualValue = element.Patterns.Value.Pattern.Value.ValueOrDefault;
            if (!string.Equals(actualValue, request.Value, StringComparison.Ordinal))
            {
                throw new AutomationOperationException(ToolErrorCode.AutomationFailure, "The target element did not accept the requested value.");
            }

            return new ActionExecutionResult(before, _inspector.Snapshot(element, includeChildren: false));
        }, cancellationToken);
    }

    public Task<ToolResult<ActionExecutionResult>> InvokeAsync(
        AutomationSessionManager sessionManager,
        InvokeRequest request,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            VerifySession(session, request.SessionId);
            var element = ResolveRequired(session, request.Target);
            var before = _inspector.Snapshot(element, includeChildren: false);

            if (!element.IsEnabled)
            {
                throw new AutomationOperationException(ToolErrorCode.ElementDisabled, "The target element is disabled.");
            }

            if (!element.Patterns.Invoke.IsSupported)
            {
                throw new AutomationOperationException(ToolErrorCode.UnsupportedPattern, "The target element does not support Invoke.");
            }

            element.Patterns.Invoke.Pattern.Invoke();
            return new ActionExecutionResult(before, _inspector.Snapshot(element, includeChildren: false));
        }, cancellationToken);
    }

    public Task<ToolResult<ActionExecutionResult>> SelectItemAsync(
        AutomationSessionManager sessionManager,
        SelectItemRequest request,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            VerifySession(session, request.SessionId);
            var container = ResolveRequired(session, request.Target);
            var before = _inspector.Snapshot(container, includeChildren: false);

            if (!container.IsEnabled)
            {
                throw new AutomationOperationException(ToolErrorCode.ElementDisabled, "The selection container is disabled.");
            }

            if (!container.Patterns.Selection.IsSupported)
            {
                throw new AutomationOperationException(ToolErrorCode.UnsupportedPattern, "The target element does not support selection.");
            }

            var items = container.FindAllDescendants(condition => condition.ByAutomationId(request.ItemAutomationId)).ToArray();
            if (items.Length == 0)
            {
                throw new AutomationOperationException(ToolErrorCode.ElementNotFound, "The requested selection item was not found.");
            }

            if (items.Length > 1)
            {
                throw new AutomationOperationException(ToolErrorCode.AmbiguousElement, "More than one selection item matched the requested identifier.");
            }

            var item = items[0];
            if (!item.Patterns.SelectionItem.IsSupported)
            {
                throw new AutomationOperationException(ToolErrorCode.UnsupportedPattern, "The item does not support selection.");
            }

            item.Patterns.SelectionItem.Pattern.Select();
            if (!item.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
            {
                throw new AutomationOperationException(ToolErrorCode.AutomationFailure, "The requested item was not selected.");
            }

            return new ActionExecutionResult(before, _inspector.Snapshot(container, includeChildren: false));
        }, cancellationToken);
    }

    private AutomationElement ResolveRequired(AutomationSession session, ElementLocator locator)
    {
        _faultInjector.ThrowIfConfigured(AutomationFaultPoint.ResolveElement);
        var resolution = ElementResolver.Resolve(session, locator);
        return resolution.Element ?? throw new AutomationOperationException(
            resolution.ErrorCode ?? ToolErrorCode.AutomationFailure,
            resolution.Message ?? "Element resolution failed.");
    }

    private static void VerifySession(AutomationSession session, string sessionId)
    {
        if (!string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new AutomationOperationException(ToolErrorCode.NoActiveSession, "The request does not reference the active session.");
        }
    }
}
