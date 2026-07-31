using System.Text.Json;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.UnitTests;

public sealed class SchemaAndConfigurationTests
{
    [Theory]
    [InlineData("test-plan.schema.json")]
    [InlineData("evidence-event.schema.json")]
    public void SchemaIsWellFormedJson(string fileName)
    {
        var path = Path.Combine(FindRepositoryRoot(), "schemas", fileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        Assert.Equal("object", document.RootElement.GetProperty("type").GetString());
        Assert.True(document.RootElement.TryGetProperty("required", out _));
    }

    [Fact]
    public void SafeSampleConfigurationDeserializes()
    {
        var path = Path.Combine(FindRepositoryRoot(), "config", "automation.sample.json");
        var configuration = ContractsJson.Deserialize<AutomationConfiguration>(File.ReadAllText(path));

        Assert.NotNull(configuration);
        Assert.True(configuration.Applications.ContainsKey("patient-demo"));
        Assert.Equal("artifacts", configuration.Evidence.RootDirectory);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "WpfAiAutomation.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Unable to find the repository root.");
    }
}
