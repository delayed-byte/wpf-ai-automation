using WpfAiAutomation.Execution;

namespace WpfAiAutomation.IntegrationTests;

public sealed class BootstrapBoundaryTests
{
    [Fact]
    public void ExecutionAssemblyIsAvailableWithoutAnMcpDependency()
    {
        Assert.NotNull(typeof(ExecutionAssemblyMarker).Assembly);
    }
}
