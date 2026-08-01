using AgentServer;

namespace WpfAiAutomation.UnitTests;

public sealed class ConfigurationPathResolverTests
{
    [Fact]
    public void FindsConfigurationRelativeToTheServerBaseDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var serverDirectory = Path.Combine(root, "apps", "AgentServer", "bin");
        var configurationPath = Path.Combine(root, "config", "automation.local.json");

        Directory.CreateDirectory(serverDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(configurationPath)!);
        File.WriteAllText(configurationPath, "{}");

        try
        {
            var result = ConfigurationPathResolver.FindConfigurationPath(baseDirectory: serverDirectory);

            Assert.Equal(NormalizePath(configurationPath), NormalizePath(result));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrefersAnExplicitExistingConfigurationPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuredPath = Path.Combine(root, "custom.json");

        Directory.CreateDirectory(root);
        File.WriteAllText(configuredPath, "{}");

        try
        {
            var result = ConfigurationPathResolver.FindConfigurationPath(configuredPath, Path.Combine(root, "server"));

            Assert.Equal(NormalizePath(configuredPath), NormalizePath(result));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string? NormalizePath(string? path) => path?
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}
