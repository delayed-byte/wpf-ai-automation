namespace WpfAiAutomation.Contracts;

public sealed record ApplicationLaunchRequest(
    string ApplicationKey,
    IReadOnlyList<string>? Arguments = null,
    int StartupTimeoutMilliseconds = 10_000);

public sealed record AutomationSessionInfo(
    string SessionId,
    int ProcessId,
    string? ApplicationVersion,
    ElementSnapshot MainWindow,
    DateTimeOffset StartedAtUtc);

public sealed record ElementLocator(
    string AutomationId,
    AutomationControlType? ControlType = null,
    string? AncestorAutomationId = null,
    int TimeoutMilliseconds = 5_000);

public sealed record ElementSnapshot(
    string AutomationId,
    string? Name,
    AutomationControlType ControlType,
    bool IsEnabled,
    bool IsVisible,
    IReadOnlyList<string> SupportedPatterns,
    string? Value,
    IReadOnlyList<ElementSnapshot>? Children = null,
    bool IsTruncated = false);

public sealed record UiTreeRequest(
    string SessionId,
    ElementLocator? Root = null,
    int MaximumDepth = 4,
    int MaximumElementCount = 200,
    bool IncludeOffScreen = false);

public sealed record FindElementRequest(string SessionId, ElementLocator Target);

public sealed record SetTextRequest(string SessionId, ElementLocator Target, string Value);

public sealed record InvokeRequest(string SessionId, ElementLocator Target);

public sealed record SelectItemRequest(string SessionId, ElementLocator Target, string ItemAutomationId);

public sealed record ReadElementRequest(string SessionId, ElementLocator Target, AutomationProperty Property);

public sealed record WaitForRequest(string SessionId, ElementLocator Target, WaitCondition Condition);

public sealed record CaptureScreenshotRequest(
    string SessionId,
    string RunId,
    string TestId,
    int StepNumber,
    string CorrelationId);

public sealed record WaitCondition(
    AutomationProperty Property,
    ComparisonOperator Operator,
    string? ExpectedValue = null,
    int PollIntervalMilliseconds = 100,
    int TimeoutMilliseconds = 5_000);

public sealed record AssertionRequest(
    string SessionId,
    ElementLocator Target,
    AutomationProperty Property,
    ComparisonOperator Operator,
    string? ExpectedValue = null,
    int TimeoutMilliseconds = 5_000);

public sealed record EvidenceReference(string RelativePath, string Kind);

public sealed record ToolResult<T>(
    bool Succeeded,
    T? Value,
    ToolErrorCode? ErrorCode,
    string? Message,
    long DurationMilliseconds,
    string CorrelationId,
    IReadOnlyList<EvidenceReference>? Evidence = null);
