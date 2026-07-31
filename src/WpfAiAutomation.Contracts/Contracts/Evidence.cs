namespace WpfAiAutomation.Contracts;

public sealed record EvidenceEvent(
    string RunId,
    string TestId,
    int StepNumber,
    AutomationAction Action,
    ElementSnapshot? ResolvedElement,
    ElementSnapshot? Before,
    ElementSnapshot? After,
    EvidenceResult Result,
    ToolErrorCode? ErrorCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMilliseconds,
    IReadOnlyList<EvidenceReference>? Artifacts = null,
    string? CorrelationId = null);
