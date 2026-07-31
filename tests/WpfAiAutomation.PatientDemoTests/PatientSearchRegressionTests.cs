using WpfAiAutomation.Contracts;
using WpfAiAutomation.PatientDemoTests.Fixtures;

namespace WpfAiAutomation.PatientDemoTests;

public sealed class PatientSearchRegressionTests(PatientDemoFixture fixture) : IClassFixture<PatientDemoFixture>
{
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(5);

    [PatientDemoFact]
    [Trait("Category", "Smoke")]
    public Task SearchForExistingPatientDisplaysSingleMatch() => fixture.RunWithFailureEvidenceAsync(
        "search-existing-patient",
        async page =>
        {
            await page.EnterPatientIdAsync("PM-1001");
            await page.SearchAsync();

            Assert.Equal("1 patient found.", await page.WaitForSearchResultAsync("1 patient found.", SearchTimeout));
        });

    [PatientDemoFact]
    [Trait("Category", "Regression")]
    public Task SearchForUnknownPatientDisplaysNoMatches() => fixture.RunWithFailureEvidenceAsync(
        "search-unknown-patient",
        async page =>
        {
            await page.EnterPatientIdAsync("PM-9999");
            await page.SearchAsync();

            Assert.Equal("0 patients found.", await page.WaitForSearchResultAsync("0 patients found.", SearchTimeout));
        });

    [PatientDemoFact]
    [Trait("Category", "Regression")]
    public Task SearchWithEmptyPatientIdReturnsAllSeedPatients() => fixture.RunWithFailureEvidenceAsync(
        "search-empty-patient-id",
        async page =>
        {
            await page.EnterPatientIdAsync(string.Empty);
            await page.SearchAsync();

            Assert.Equal("3 patients found.", await page.WaitForSearchResultAsync("3 patients found.", SearchTimeout));
        });

    [PatientDemoFact]
    [Trait("Category", "Regression")]
    public Task WaitForSearchResultReportsBoundedTimeoutWhenExpectedStateNeverArrives() => fixture.RunWithFailureEvidenceAsync(
        "search-bounded-timeout",
        async page =>
        {
            await page.EnterPatientIdAsync("PM-1001");
            await page.SearchAsync();

            var exception = await Assert.ThrowsAsync<PageObjects.PatientSearchPageException>(
                () => page.WaitForSearchResultAsync("Unexpected result", TimeSpan.FromMilliseconds(50)));
            Assert.Equal(ToolErrorCode.ConditionTimeout, exception.ErrorCode);

            Assert.Equal("1 patient found.", await page.WaitForSearchResultAsync("1 patient found.", SearchTimeout));
        });
}
