using WpfAiAutomation.Contracts;
using WpfAiAutomation.FlaUI;

namespace WpfAiAutomation.UnitTests.FlaUI;

public sealed class DeterministicAutomationFaultInjectorTests
{
    [Theory]
    [InlineData(AutomationFaultPoint.ResolveElement, ToolErrorCode.AmbiguousElement)]
    [InlineData(AutomationFaultPoint.ResolveElement, ToolErrorCode.AutomationFailure)]
    [InlineData(AutomationFaultPoint.WaitForCondition, ToolErrorCode.ConditionTimeout)]
    [InlineData(AutomationFaultPoint.CaptureScreenshot, ToolErrorCode.AutomationFailure)]
    public void ConfiguredFaultsThrowTheirStableErrorCode(AutomationFaultPoint point, ToolErrorCode errorCode)
    {
        var injector = new DeterministicAutomationFaultInjector(new Dictionary<AutomationFaultPoint, ToolErrorCode>
        {
            [point] = errorCode,
        });

        var exception = Assert.ThrowsAny<Exception>(() => injector.ThrowIfConfigured(point));

        Assert.Contains(errorCode.ToString(), exception.Message, StringComparison.Ordinal);
    }
}
