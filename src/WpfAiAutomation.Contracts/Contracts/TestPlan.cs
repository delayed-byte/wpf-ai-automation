using System.Text.Json.Serialization;

namespace WpfAiAutomation.Contracts;

public sealed record TestPlan(
    string SchemaVersion,
    string TestId,
    string ApplicationKey,
    IReadOnlyDictionary<string, string>? TestData,
    IReadOnlyList<TestStep> Steps);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "action")]
[JsonDerivedType(typeof(SetTextStep), "setText")]
[JsonDerivedType(typeof(InvokeStep), "invoke")]
[JsonDerivedType(typeof(SelectItemStep), "selectItem")]
[JsonDerivedType(typeof(ReadElementStep), "readElement")]
[JsonDerivedType(typeof(WaitForStep), "waitFor")]
[JsonDerivedType(typeof(AssertStep), "assert")]
[JsonDerivedType(typeof(CaptureScreenshotStep), "captureScreenshot")]
public abstract record TestStep(int StepNumber);

public sealed record SetTextStep(
    int StepNumber,
    ElementLocator Target,
    string Value,
    int TimeoutMilliseconds = 5_000) : TestStep(StepNumber);

public sealed record InvokeStep(
    int StepNumber,
    ElementLocator Target,
    int TimeoutMilliseconds = 5_000) : TestStep(StepNumber);

public sealed record SelectItemStep(
    int StepNumber,
    ElementLocator Target,
    string ItemAutomationId,
    int TimeoutMilliseconds = 5_000) : TestStep(StepNumber);

public sealed record ReadElementStep(
    int StepNumber,
    ElementLocator Target,
    AutomationProperty Property,
    int TimeoutMilliseconds = 5_000) : TestStep(StepNumber);

public sealed record WaitForStep(
    int StepNumber,
    ElementLocator Target,
    WaitCondition Condition) : TestStep(StepNumber);

public sealed record AssertStep(
    int StepNumber,
    ElementLocator Target,
    AutomationProperty Property,
    ComparisonOperator Operator,
    string? ExpectedValue = null,
    int TimeoutMilliseconds = 5_000) : TestStep(StepNumber);

public sealed record CaptureScreenshotStep(
    int StepNumber,
    string Name) : TestStep(StepNumber);
