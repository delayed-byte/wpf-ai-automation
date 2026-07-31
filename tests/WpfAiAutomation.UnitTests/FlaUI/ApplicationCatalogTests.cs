using WpfAiAutomation.Contracts;
using WpfAiAutomation.FlaUI;

namespace WpfAiAutomation.UnitTests.FlaUI;

public sealed class ApplicationCatalogTests
{
    [Fact]
    public void ResolveAcceptsOnlyConfiguredApplicationArgumentsAndTimeout()
    {
        var catalog = new ApplicationCatalog(CreateConfiguration());
        var request = new ApplicationLaunchRequest("patient-demo", ["--test-mode"], 5_000);

        var resolved = catalog.TryResolve(request, out var application, out var errorCode, out var message);

        Assert.True(resolved, message);
        Assert.NotNull(application);
        Assert.Equal("PatientDemo", application.ProcessName);
        Assert.Equal(default, errorCode);
    }

    [Fact]
    public void ResolveRejectsUnknownApplication()
    {
        var catalog = new ApplicationCatalog(CreateConfiguration());

        var resolved = catalog.TryResolve(new ApplicationLaunchRequest("unapproved", [], 5_000), out _, out var errorCode, out _);

        Assert.False(resolved);
        Assert.Equal(ToolErrorCode.ApplicationNotAllowed, errorCode);
    }

    [Fact]
    public void ResolveRejectsUnapprovedArgumentAndExcessiveTimeout()
    {
        var catalog = new ApplicationCatalog(CreateConfiguration());

        var argumentResolved = catalog.TryResolve(new ApplicationLaunchRequest("patient-demo", ["--unsafe"], 5_000), out _, out var argumentError, out _);
        var timeoutResolved = catalog.TryResolve(new ApplicationLaunchRequest("patient-demo", [], 10_001), out _, out var timeoutError, out _);

        Assert.False(argumentResolved);
        Assert.Equal(ToolErrorCode.ApplicationNotAllowed, argumentError);
        Assert.False(timeoutResolved);
        Assert.Equal(ToolErrorCode.InvalidRequest, timeoutError);
    }

    [Fact]
    public void CanonicalPathsCompareWithoutCaseOrTrailingSeparatorDifferences()
    {
        Assert.True(ApplicationCatalog.CanonicalPathsEqual(@"C:\Automation\PatientDemo", "c:\\automation\\patientdemo\\"));
    }

    private static AutomationConfiguration CreateConfiguration() => new(
        new Dictionary<string, ApplicationCatalogEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["patient-demo"] = new(@"C:\Automation\PatientDemo.exe", "PatientDemo", ["--test-mode"], 10_000),
        },
        PolicyCeilings.Default,
        new SensitiveControlConfiguration([]),
        new EvidencePathConfiguration("artifacts"));
}
