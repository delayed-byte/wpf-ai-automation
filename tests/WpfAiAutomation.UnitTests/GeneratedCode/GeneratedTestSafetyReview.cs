using System.Text.RegularExpressions;

namespace WpfAiAutomation.UnitTests.GeneratedCode;

internal static partial class GeneratedTestSafetyReview
{
    private static readonly (string Rule, Regex Pattern)[] RejectedPatterns =
    [
        ("fixed sleeps", FixedSleepPattern()),
        ("coordinate input", CoordinateInputPattern()),
        ("absolute paths", AbsolutePathPattern()),
        ("direct FlaUI access", DirectFlaUiPattern()),
        ("generated timestamps or identifiers", NondeterministicIdentifierPattern()),
        ("restricted patient-data mutations", RestrictedMutationPattern()),
    ];

    public static IReadOnlyList<string> Review(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issues = RejectedPatterns
            .Where(rule => rule.Pattern.IsMatch(source))
            .Select(rule => $"Generated tests must not contain {rule.Rule}.")
            .ToList();

        if (!source.Contains("[Trait(\"Category\",", StringComparison.Ordinal))
        {
            issues.Add("Generated tests must declare a stable test category.");
        }

        if (!source.Contains("PatientSearchPage", StringComparison.Ordinal))
        {
            issues.Add("Generated Patient Search tests must use the reviewed page object.");
        }

        if (!source.Contains("TimeSpan.From", StringComparison.Ordinal))
        {
            issues.Add("Generated tests must use an explicit bounded timeout.");
        }

        return issues;
    }

    [GeneratedRegex(@"\b(?:Thread\.Sleep|Task\.Delay)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex FixedSleepPattern();

    [GeneratedRegex(@"\b(?:Mouse|Keyboard|Point|Coordinates?)\b|\.Click\s*\(\s*\d+\s*,", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CoordinateInputPattern();

    [GeneratedRegex(@"(?:[A-Za-z]:\\+|file://|/home/|/Users/)", RegexOptions.CultureInvariant)]
    private static partial Regex AbsolutePathPattern();

    [GeneratedRegex(@"\b(?:FlaUI|AutomationElement|UIA3Automation)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DirectFlaUiPattern();

    [GeneratedRegex(@"\b(?:DateTime(?:Offset)?\.(?:Now|UtcNow)|Guid\.NewGuid)\b", RegexOptions.CultureInvariant)]
    private static partial Regex NondeterministicIdentifierPattern();

    [GeneratedRegex(@"\b(?:Delete|ConfirmDelete|Update|AddPatient)Async\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RestrictedMutationPattern();
}
