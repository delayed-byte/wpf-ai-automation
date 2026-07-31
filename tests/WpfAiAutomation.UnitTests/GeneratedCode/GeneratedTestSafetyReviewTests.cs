namespace WpfAiAutomation.UnitTests.GeneratedCode;

public sealed class GeneratedTestSafetyReviewTests
{
    [Fact]
    public void ReviewAcceptsProposalWhenItUsesReviewedConventions()
    {
        const string source = """
            [Fact]
            [Trait("Category", "Smoke")]
            public async Task SearchForExistingPatient_DisplaysSingleMatch()
            {
                PatientSearchPage page = fixture.Page;
                await page.WaitForSearchResultAsync("1 patient found.", TimeSpan.FromSeconds(5));
            }
            """;

        Assert.Empty(GeneratedTestSafetyReview.Review(source));
    }

    [Fact]
    public void ReviewReportsEverySeededViolationWhenProposalIsUnsafeAndFlaky()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Generated", "seeded-unsafe-example.cs.txt"));

        var issues = GeneratedTestSafetyReview.Review(source);

        Assert.Contains(issues, issue => issue.Contains("fixed sleeps", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("coordinate input", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("absolute paths", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("direct FlaUI access", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("generated timestamps", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("stable test category", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("reviewed page object", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("bounded timeout", StringComparison.Ordinal));
    }

    [Fact]
    public void ReviewAcceptsGeneratedExistingPatientProposal()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Generated", "existing-patient-proposal.cs.txt"));

        Assert.Empty(GeneratedTestSafetyReview.Review(source));
    }
}
