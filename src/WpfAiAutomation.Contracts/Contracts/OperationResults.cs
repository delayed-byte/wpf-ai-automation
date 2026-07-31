namespace WpfAiAutomation.Contracts;

public sealed record AutomationOperationContext(
    string RunId,
    string TestId,
    int StepNumber,
    string CorrelationId);

public sealed record ActionExecutionResult(
    ElementSnapshot Before,
    ElementSnapshot After,
    bool UsedFallback = false);

public sealed record ObservationResult(
    ElementSnapshot? Element,
    AutomationProperty Property,
    string? Value,
    bool Exists);

public sealed record AssertionResult(
    bool Passed,
    AutomationProperty Property,
    ComparisonOperator Operator,
    string? ExpectedValue,
    string? ActualValue,
    ElementSnapshot? Element);

public sealed record EvidenceRunSummary(
    string RunId,
    string TestId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int PassedSteps,
    int FailedSteps,
    int SkippedSteps,
    string RelativeDirectory);

public sealed record PlanStepResult(
    int StepNumber,
    AutomationAction Action,
    EvidenceResult Result,
    ToolErrorCode? ErrorCode,
    string? Message,
    long DurationMilliseconds,
    string CorrelationId,
    IReadOnlyList<EvidenceReference>? Evidence = null);

public sealed record PlanExecutionSummary(
    string RunId,
    string TestId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int PassedSteps,
    int FailedSteps,
    int SkippedSteps,
    IReadOnlyList<PlanStepResult> Steps,
    EvidenceRunSummary Evidence);
