using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

/// <summary>
/// Test adapter for exercising deterministic UI-automation failure paths without
/// depending on timing, duplicate controls, or a real stale UIA element.
/// Production composition uses the empty injector.
/// </summary>
public sealed class DeterministicAutomationFaultInjector
{
    private readonly IReadOnlyDictionary<AutomationFaultPoint, ToolErrorCode> _faults;

    public DeterministicAutomationFaultInjector(IReadOnlyDictionary<AutomationFaultPoint, ToolErrorCode>? faults = null)
    {
        _faults = faults ?? new Dictionary<AutomationFaultPoint, ToolErrorCode>();
    }

    public void ThrowIfConfigured(AutomationFaultPoint point)
    {
        if (_faults.TryGetValue(point, out var errorCode))
        {
            throw new AutomationOperationException(errorCode, $"A deterministic {point} fault ({errorCode}) was injected for testing.");
        }
    }
}

public enum AutomationFaultPoint
{
    ResolveElement,
    WaitForCondition,
    CaptureScreenshot,
}
