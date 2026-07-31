using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;

namespace WpfAiAutomation.UnitTests;

public sealed class TestPlanValidatorTests
{
    [Fact]
    public void RejectsUndeclaredAndExpressionLikeVariables()
    {
        var plan = Plan(new SetTextStep(1, Locator(), "${missing}-${patientId.ToUpper()}"));
        var validation = new TestPlanValidator(PolicyCeilings.Default).Validate(plan);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Message.Contains("missing", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Message.Contains("exact ${name}", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesOnlyDeclaredTestData()
    {
        var plan = Plan(new SetTextStep(1, Locator(), "prefix-${patientId}"));
        var resolved = new TestPlanValidator(PolicyCeilings.Default).ResolveVariables(plan);

        Assert.Equal("prefix-PM-1001", Assert.IsType<SetTextStep>(resolved.Steps[0]).Value);
    }

    [Fact]
    public void RejectsCombinedTimeoutAboveConfiguredCeiling()
    {
        var ceilings = PolicyCeilings.Default with { MaximumTotalTimeoutMilliseconds = 1_000 };
        var plan = Plan(
            new InvokeStep(1, Locator(), 600),
            new InvokeStep(2, Locator("second"), 600));

        var validation = new TestPlanValidator(ceilings).Validate(plan);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Message.Contains("combined step timeout", StringComparison.Ordinal));
    }

    private static TestPlan Plan(params TestStep[] steps) => new(
        "1.0",
        "patient-search",
        "patient-demo",
        new Dictionary<string, string> { ["patientId"] = "PM-1001" },
        steps);

    private static ElementLocator Locator(string automationId = "PatientIdTextBox") => new(automationId);
}
