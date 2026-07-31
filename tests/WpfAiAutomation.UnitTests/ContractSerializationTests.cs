using System.Text.Json;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.UnitTests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void ValidTestPlanRoundTripsAndPassesContractValidation()
    {
        var plan = DeserializeFixture<TestPlan>("test-plan.valid.json");
        var validation = ContractValidators.Validate(plan);
        var roundTripped = ContractsJson.Deserialize<TestPlan>(ContractsJson.Serialize(plan));

        Assert.True(validation.IsValid, FormatErrors(validation));
        Assert.NotNull(roundTripped);
        Assert.Equal(plan.TestId, roundTripped.TestId);
        Assert.Equal(plan.ApplicationKey, roundTripped.ApplicationKey);
        Assert.Collection(
            roundTripped.Steps,
            step => Assert.IsType<SetTextStep>(step),
            step => Assert.IsType<InvokeStep>(step),
            step => Assert.IsType<WaitForStep>(step),
            step => Assert.IsType<AssertStep>(step));
    }

    [Fact]
    public void ValidEvidenceEventRoundTripsAndPassesContractValidation()
    {
        var evidence = DeserializeFixture<EvidenceEvent>("evidence-event.valid.json");
        var validation = ContractValidators.Validate(evidence);
        var roundTripped = ContractsJson.Deserialize<EvidenceEvent>(ContractsJson.Serialize(evidence));

        Assert.True(validation.IsValid, FormatErrors(validation));
        Assert.NotNull(roundTripped);
        Assert.Equal(evidence.RunId, roundTripped.RunId);
        Assert.Equal(EvidenceResult.Passed, roundTripped.Result);
        Assert.Single(roundTripped.Artifacts!);
    }

    [Fact]
    public void InvalidFixtureWithMissingAutomationIdIsRejectedByContractValidation()
    {
        var plan = DeserializeFixture<TestPlan>("test-plan.invalid-missing-automation-id.json");
        var validation = ContractValidators.Validate(plan);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "automationId");
    }

    [Fact]
    public void InvalidFixtureWithUnknownActionIsRejectedDuringDeserialization()
    {
        Assert.Throws<JsonException>(() => ContractsJson.Deserialize<TestPlan>(ReadFixture("test-plan.invalid-unknown-action.json")));
    }

    [Fact]
    public void InvalidFixtureWithNegativeEvidenceDurationIsRejectedByContractValidation()
    {
        var evidence = DeserializeFixture<EvidenceEvent>("evidence-event.invalid-negative-duration.json");
        var validation = ContractValidators.Validate(evidence);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "durationMilliseconds");
    }

    [Fact]
    public void UnknownJsonMembersAreRejected()
    {
        const string Json = """
            { "applicationKey": "patient-demo", "unexpected": true }
            """;

        Assert.Throws<JsonException>(() => ContractsJson.Deserialize<ApplicationLaunchRequest>(Json));
    }

    private static T DeserializeFixture<T>(string name)
    {
        var value = ContractsJson.Deserialize<T>(ReadFixture(name));
        return Assert.IsType<T>(value);
    }

    private static string ReadFixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string FormatErrors(ValidationResult result) => string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Field}: {error.Message}"));
}
