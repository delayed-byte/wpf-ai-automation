namespace AgentServer;

public static class ConfigurationPathResolver
{
    public const string RelativeConfigurationPath = "config/automation.local.json";
    public const string ConfigurationPathVariable = "WPF_AI_AUTOMATION_CONFIG";

    public static string? FindConfigurationPath(string? configuredPath = null, string? baseDirectory = null)
    {
        configuredPath ??= Environment.GetEnvironmentVariable(ConfigurationPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        for (var directory = new DirectoryInfo(baseDirectory ?? AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, RelativeConfigurationPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
