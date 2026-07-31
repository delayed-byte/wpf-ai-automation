using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed record ApprovedApplication(
    string Key,
    string ExecutablePath,
    string ProcessName,
    IReadOnlyList<string> AllowedArguments,
    int StartupTimeoutCeilingMilliseconds,
    int CloseTimeoutMilliseconds,
    bool AllowForcedTermination,
    string? VersionProbe);

public sealed class ApplicationCatalog
{
    private readonly Dictionary<string, ApprovedApplication> _applications;

    public ApplicationCatalog(AutomationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var applications = new Dictionary<string, ApprovedApplication>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration.Applications)
        {
            applications.Add(pair.Key, CreateApprovedApplication(pair.Key, pair.Value));
        }

        _applications = applications;
    }

    public static ApplicationCatalog Load(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        var configuration = ContractsJson.Deserialize<AutomationConfiguration>(File.ReadAllText(configurationPath));
        return new ApplicationCatalog(configuration ?? throw new InvalidOperationException("Automation configuration is empty."));
    }

    public bool TryResolve(
        ApplicationLaunchRequest request,
        out ApprovedApplication? application,
        out ToolErrorCode errorCode,
        out string message)
    {
        application = null;
        errorCode = ToolErrorCode.InvalidRequest;
        message = "Application key is required.";

        if (request is null || string.IsNullOrWhiteSpace(request.ApplicationKey))
        {
            return false;
        }

        if (!_applications.TryGetValue(request.ApplicationKey, out var configuredApplication))
        {
            errorCode = ToolErrorCode.ApplicationNotAllowed;
            message = "The requested application is not allowlisted.";
            return false;
        }

        if (request.StartupTimeoutMilliseconds is < 1 or > 60_000 || request.StartupTimeoutMilliseconds > configuredApplication.StartupTimeoutCeilingMilliseconds)
        {
            message = "Startup timeout exceeds the configured ceiling.";
            return false;
        }

        foreach (var argument in request.Arguments ?? Array.Empty<string>())
        {
            if (!configuredApplication.AllowedArguments.Contains(argument, StringComparer.Ordinal))
            {
                errorCode = ToolErrorCode.ApplicationNotAllowed;
                message = "One or more application arguments are not allowlisted.";
                return false;
            }
        }

        application = configuredApplication;
        errorCode = default;
        message = string.Empty;
        return true;
    }

    public static bool CanonicalPathsEqual(string left, string right) =>
        string.Equals(CanonicalizePath(left), CanonicalizePath(right), StringComparison.OrdinalIgnoreCase);

    public static string CanonicalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static ApprovedApplication CreateApprovedApplication(string key, ApplicationCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(key) || !Path.IsPathFullyQualified(entry.ExecutablePath))
        {
            throw new ArgumentException("Application keys and executable paths must be configured with non-empty, absolute values.");
        }

        if (string.IsNullOrWhiteSpace(entry.ProcessName))
        {
            throw new ArgumentException("A configured application must have a process name.");
        }

        if (entry.StartupTimeoutCeilingMilliseconds is < 1 or > 60_000 || entry.CloseTimeoutMilliseconds is < 1 or > 60_000)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "Configured session timeouts must be between 1 and 60000 milliseconds.");
        }

        return new ApprovedApplication(
            key,
            CanonicalizePath(entry.ExecutablePath),
            entry.ProcessName,
            entry.AllowedArguments ?? Array.Empty<string>(),
            entry.StartupTimeoutCeilingMilliseconds,
            entry.CloseTimeoutMilliseconds,
            entry.AllowForcedTermination,
            entry.VersionProbe);
    }
}
