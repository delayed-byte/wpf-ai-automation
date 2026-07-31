using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed class UiInspector
{
    private readonly SensitiveDataRedactor _redactor;
    private readonly PolicyCeilings _ceilings;

    public UiInspector(SensitiveDataRedactor redactor, PolicyCeilings ceilings)
    {
        _redactor = redactor;
        _ceilings = ceilings;
    }

    public Task<ToolResult<ElementSnapshot>> FindAsync(AutomationSessionManager sessionManager, FindElementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            if (!string.Equals(session.SessionId, request.SessionId, StringComparison.Ordinal))
            {
                throw new AutomationOperationException(ToolErrorCode.NoActiveSession, "The request does not reference the active session.");
            }

            var resolution = ElementResolver.Resolve(session, request.Target);
            if (!resolution.Succeeded)
            {
                throw new AutomationOperationException(resolution.ErrorCode ?? ToolErrorCode.AutomationFailure, resolution.Message ?? "Element resolution failed.");
            }

            return Snapshot(resolution.Element!, includeChildren: false);
        }, cancellationToken);
    }

    public Task<ToolResult<ElementSnapshot>> InspectAsync(AutomationSessionManager sessionManager, UiTreeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            if (!string.Equals(session.SessionId, request.SessionId, StringComparison.Ordinal))
            {
                throw new AutomationOperationException(ToolErrorCode.NoActiveSession, "The request does not reference the active session.");
            }

            var root = request.Root is null ? session.MainWindow : ResolveRequired(session, request.Root);
            var maximumDepth = Math.Min(request.MaximumDepth, _ceilings.MaximumTreeDepth);
            var maximumElementCount = Math.Min(request.MaximumElementCount, _ceilings.MaximumTreeElementCount);
            if (maximumDepth < 0 || maximumElementCount < 1)
            {
                throw new AutomationOperationException(ToolErrorCode.InvalidRequest, "Tree limits are invalid.");
            }

            var counter = new ElementCounter(maximumElementCount);
            return BuildTree(root, maximumDepth, request.IncludeOffScreen, counter, isSensitiveAncestor: false);
        }, cancellationToken);
    }

    internal ElementSnapshot Snapshot(AutomationElement element, bool includeChildren)
    {
        var counter = new ElementCounter(includeChildren ? _ceilings.MaximumTreeElementCount : 1);
        return BuildTree(element, includeChildren ? _ceilings.MaximumTreeDepth : 0, includeOffScreen: true, counter, isSensitiveAncestor: false);
    }

    private static AutomationElement ResolveRequired(AutomationSession session, ElementLocator locator)
    {
        var resolution = ElementResolver.Resolve(session, locator);
        return resolution.Element ?? throw new AutomationOperationException(resolution.ErrorCode ?? ToolErrorCode.AutomationFailure, resolution.Message ?? "Element resolution failed.");
    }

    private ElementSnapshot BuildTree(
        AutomationElement element,
        int remainingDepth,
        bool includeOffScreen,
        ElementCounter counter,
        bool isSensitiveAncestor)
    {
        counter.AddElement();

        var automationId = SafeRead(() => element.AutomationId, string.Empty);
        var isSensitive = isSensitiveAncestor || _redactor.IsSensitive(automationId);

        var children = new List<ElementSnapshot>();
        var availableChildren = element.FindAllChildren()
            .Where(child => includeOffScreen || !SafeRead(() => child.IsOffscreen, fallback: true))
            .ToArray();
        var truncated = remainingDepth == 0 && availableChildren.Length > 0;
        if (remainingDepth > 0)
        {
            foreach (var child in availableChildren)
            {
                if (counter.IsAtLimit)
                {
                    truncated = true;
                    break;
                }

                children.Add(BuildTree(child, remainingDepth - 1, includeOffScreen, counter, isSensitive));
            }
        }

        return new ElementSnapshot(
            automationId,
            _redactor.Redact(automationId, SafeRead(() => element.Name, null), isSensitiveAncestor),
            ElementResolver.ToContractControlType(element),
            SafeRead(() => element.IsEnabled, fallback: false),
            !SafeRead(() => element.IsOffscreen, fallback: true),
            GetSupportedPatterns(element),
            _redactor.Redact(automationId, GetNormalizedValue(element), isSensitiveAncestor),
            children,
            truncated);
    }

    private static List<string> GetSupportedPatterns(AutomationElement element)
    {
        var patterns = new List<string>();
        if (SafeRead(() => element.Patterns.Value.IsSupported, fallback: false)) patterns.Add("Value");
        if (SafeRead(() => element.Patterns.Text.IsSupported, fallback: false)) patterns.Add("Text");
        if (SafeRead(() => element.Patterns.Selection.IsSupported, fallback: false)) patterns.Add("Selection");
        if (SafeRead(() => element.Patterns.Toggle.IsSupported, fallback: false)) patterns.Add("Toggle");
        if (SafeRead(() => element.Patterns.ExpandCollapse.IsSupported, fallback: false)) patterns.Add("ExpandCollapse");
        if (SafeRead(() => element.Patterns.Invoke.IsSupported, fallback: false)) patterns.Add("Invoke");
        return patterns;
    }

    private static string? GetNormalizedValue(AutomationElement element)
    {
        if (SafeRead(() => element.Patterns.Value.IsSupported, fallback: false))
        {
            return SafeRead(() => element.Patterns.Value.Pattern.Value.ValueOrDefault, null);
        }

        if (SafeRead(() => element.Patterns.Text.IsSupported, fallback: false))
        {
            return SafeRead(() => element.Patterns.Text.Pattern.DocumentRange.GetText(-1), null);
        }

        if (SafeRead(() => element.Patterns.Selection.IsSupported, fallback: false))
        {
            return SafeRead(
                () => string.Join(",", (element.Patterns.Selection.Pattern.Selection.ValueOrDefault ?? Array.Empty<AutomationElement>()).Select(item => SafeRead(() => item.Name, string.Empty))),
                null);
        }

        if (SafeRead(() => element.Patterns.Toggle.IsSupported, fallback: false))
        {
            return SafeRead(() => element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault.ToString(), null);
        }

        if (SafeRead(() => element.Patterns.ExpandCollapse.IsSupported, fallback: false))
        {
            return SafeRead(() => element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault.ToString(), null);
        }

        return null;
    }

    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (PropertyNotSupportedException)
        {
            return fallback;
        }
        catch (ElementNotAvailableException)
        {
            return fallback;
        }
    }

    private sealed class ElementCounter(int maximumElementCount)
    {
        private int _count;

        public bool IsAtLimit => _count >= maximumElementCount;

        public void AddElement() => _count++;
    }

}
