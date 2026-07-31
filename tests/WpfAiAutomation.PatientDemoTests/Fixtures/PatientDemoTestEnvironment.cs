using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.PatientDemoTests.Fixtures;

internal static class PatientDemoTestEnvironment
{
    private const string RelativeConfigurationPath = "config/automation.local.json";
    private const string ConfigurationPathVariable = "WPF_AI_AUTOMATION_CONFIG";
    private const string EvidenceRootVariable = "WPF_AI_AUTOMATION_EVIDENCE_ROOT";

    public static string? FindConfigurationPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ConfigurationPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, RelativeConfigurationPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static AutomationConfiguration LoadConfiguration()
    {
        var path = FindConfigurationPath()
            ?? throw new InvalidOperationException($"Create {RelativeConfigurationPath} before running Patient Demo regression tests.");

        var configuration = ContractsJson.Deserialize<AutomationConfiguration>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The Patient Demo automation configuration is empty.");

        var evidenceRoot = Environment.GetEnvironmentVariable(EvidenceRootVariable);
        if (!string.IsNullOrWhiteSpace(evidenceRoot))
        {
            configuration = configuration with
            {
                Evidence = configuration.Evidence with { RootDirectory = Path.GetFullPath(evidenceRoot) },
            };
        }
        else if (!Path.IsPathRooted(configuration.Evidence.RootDirectory))
        {
            var repositoryRoot = Directory.GetParent(Path.GetDirectoryName(path)!)!.FullName;
            configuration = configuration with
            {
                Evidence = configuration.Evidence with
                {
                    RootDirectory = Path.GetFullPath(configuration.Evidence.RootDirectory, repositoryRoot),
                },
            };
        }

        if (!configuration.Applications.TryGetValue("patient-demo", out var application)
            || !File.Exists(application.ExecutablePath))
        {
            throw new InvalidOperationException("The configured Patient Demo executable does not exist.");
        }

        return configuration;
    }
}
