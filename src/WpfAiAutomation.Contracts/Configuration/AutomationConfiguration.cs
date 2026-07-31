namespace WpfAiAutomation.Contracts;

public sealed record AutomationConfiguration(
    IReadOnlyDictionary<string, ApplicationCatalogEntry> Applications,
    PolicyCeilings Policy,
    SensitiveControlConfiguration SensitiveControls,
    EvidencePathConfiguration Evidence);

public sealed record ApplicationCatalogEntry(
    string ExecutablePath,
    string ProcessName,
    IReadOnlyList<string> AllowedArguments,
    int StartupTimeoutCeilingMilliseconds,
    string? VersionProbe = null);

public sealed record PolicyCeilings(
    int MaximumStepCount,
    int MaximumStringLength,
    int MaximumTimeoutMilliseconds,
    int MaximumTotalTimeoutMilliseconds,
    int MaximumTreeDepth,
    int MaximumTreeElementCount)
{
    public static PolicyCeilings Default { get; } = new(
        MaximumStepCount: 100,
        MaximumStringLength: 4_096,
        MaximumTimeoutMilliseconds: 60_000,
        MaximumTotalTimeoutMilliseconds: 300_000,
        MaximumTreeDepth: 10,
        MaximumTreeElementCount: 1_000);
}

public sealed record SensitiveControlConfiguration(
    IReadOnlyList<string> AutomationIds,
    string RedactionReplacement = "[REDACTED]");

public sealed record EvidencePathConfiguration(
    string RootDirectory,
    bool CaptureScreenshotOnFailure = true);
