using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed class SensitiveDataRedactor
{
    private readonly HashSet<string> _sensitiveAutomationIds;
    private readonly string _replacement;

    public SensitiveDataRedactor(SensitiveControlConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _sensitiveAutomationIds = new HashSet<string>(configuration.AutomationIds, StringComparer.Ordinal);
        _replacement = configuration.RedactionReplacement;
    }

    public bool IsSensitive(string automationId) => _sensitiveAutomationIds.Contains(automationId);

    public string? Redact(string automationId, string? value, bool isSensitiveAncestor = false) =>
        isSensitiveAncestor || IsSensitive(automationId) ? _replacement : value;
}
