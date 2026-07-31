using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

internal sealed record ElementResolution(AutomationElement? Element, ToolErrorCode? ErrorCode, string? Message)
{
    public bool Succeeded => Element is not null;
}

internal sealed class AutomationOperationException(
    ToolErrorCode errorCode,
    string message,
    object? diagnosticValue = null) : Exception(message)
{
    public ToolErrorCode ErrorCode { get; } = errorCode;

    public object? DiagnosticValue { get; } = diagnosticValue;
}

internal static class ElementResolver
{
    public static ElementResolution Resolve(AutomationSession session, ElementLocator locator)
    {
        var validation = ContractValidators.Validate(locator);
        if (!validation.IsValid)
        {
            return new ElementResolution(null, ToolErrorCode.InvalidRequest, "The element locator is invalid.");
        }

        var rootResult = ResolveSearchRoot(session, locator.AncestorAutomationId);
        if (!rootResult.Succeeded)
        {
            return rootResult;
        }

        var searchRoot = rootResult.Element!;
        var candidates = searchRoot
            .FindAllDescendants(condition => condition.ByAutomationId(locator.AutomationId))
            .Append(searchRoot)
            .Where(element => string.Equals(element.AutomationId, locator.AutomationId, StringComparison.Ordinal))
            .Where(element => IsOwnedBySession(element, session))
            .Where(element => locator.ControlType is null || ToContractControlType(element) == locator.ControlType)
            .ToArray();

        return candidates.Length switch
        {
            0 => new ElementResolution(null, ToolErrorCode.ElementNotFound, "No element matched the exact locator."),
            1 => new ElementResolution(candidates[0], null, null),
            _ => new ElementResolution(null, ToolErrorCode.AmbiguousElement, "More than one element matched the exact locator."),
        };
    }

    private static ElementResolution ResolveSearchRoot(AutomationSession session, string? ancestorAutomationId)
    {
        if (string.IsNullOrWhiteSpace(ancestorAutomationId))
        {
            return new ElementResolution(session.MainWindow, null, null);
        }

        var ancestors = session.MainWindow
            .FindAllDescendants(condition => condition.ByAutomationId(ancestorAutomationId))
            .Append(session.MainWindow)
            .Where(element => string.Equals(element.AutomationId, ancestorAutomationId, StringComparison.Ordinal))
            .Where(element => IsOwnedBySession(element, session))
            .ToArray();

        return ancestors.Length switch
        {
            0 => new ElementResolution(null, ToolErrorCode.ElementNotFound, "The ancestor element was not found."),
            1 => new ElementResolution(ancestors[0], null, null),
            _ => new ElementResolution(null, ToolErrorCode.AmbiguousElement, "More than one ancestor element matched the locator."),
        };
    }

    internal static bool IsOwnedBySession(AutomationElement element, AutomationSession session) =>
        element.Properties.ProcessId.ValueOrDefault == session.ProcessId;

    internal static AutomationControlType ToContractControlType(AutomationElement element) => element.ControlType switch
    {
        var controlType when controlType == ControlType.Button => AutomationControlType.Button,
        var controlType when controlType == ControlType.CheckBox => AutomationControlType.CheckBox,
        var controlType when controlType == ControlType.ComboBox => AutomationControlType.ComboBox,
        var controlType when controlType == ControlType.Edit => AutomationControlType.Edit,
        var controlType when controlType == ControlType.List => AutomationControlType.List,
        var controlType when controlType == ControlType.ListItem => AutomationControlType.ListItem,
        var controlType when controlType == ControlType.Text => AutomationControlType.Text,
        var controlType when controlType == ControlType.Window => AutomationControlType.Window,
        _ => AutomationControlType.Unknown,
    };
}
