using WpfAiAutomation.Contracts;
using WpfAiAutomation.FlaUI;

namespace WpfAiAutomation.UnitTests.FlaUI;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactReplacesValuesForConfiguredSensitiveControls()
    {
        var redactor = new SensitiveDataRedactor(new SensitiveControlConfiguration(["PasswordTextBox"], "[HIDDEN]"));

        var redacted = redactor.Redact("PasswordTextBox", "secret");

        Assert.Equal("[HIDDEN]", redacted);
    }

    [Fact]
    public void RedactLeavesNonSensitiveValuesUnchanged()
    {
        var redactor = new SensitiveDataRedactor(new SensitiveControlConfiguration(["PasswordTextBox"]));

        var value = redactor.Redact("PatientNameLabel", "John Smith");

        Assert.Equal("John Smith", value);
    }

    [Fact]
    public void RedactReplacesValuesInASensitiveDescendantTree()
    {
        var redactor = new SensitiveDataRedactor(new SensitiveControlConfiguration([]));

        var redacted = redactor.Redact(string.Empty, "PM-1001", isSensitiveAncestor: true);

        Assert.Equal("[REDACTED]", redacted);
    }
}
