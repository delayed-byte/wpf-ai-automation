using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.PatientDemoTests;

public sealed class BootstrapBoundaryTests
{
    [Fact]
    public void PatientDemoTestProjectUsesTheTransportNeutralContracts()
    {
        Assert.NotNull(typeof(ElementLocator).Assembly);
    }
}
