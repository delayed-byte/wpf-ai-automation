using System.Diagnostics;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed class PatientDemoInspectionHarness : IAsyncDisposable
{
    private readonly AutomationConfiguration _configuration;
    private readonly ApplicationCatalog _catalog;
    private readonly UiInspector _inspector;
    private readonly AutomationSessionManager _sessionManager;

    public PatientDemoInspectionHarness(AutomationConfiguration configuration)
    {
        _configuration = configuration;
        _catalog = new ApplicationCatalog(configuration);
        _inspector = new UiInspector(new SensitiveDataRedactor(configuration.SensitiveControls), configuration.Policy);
        _sessionManager = new AutomationSessionManager(_catalog, _inspector);
    }

    public async Task<ToolResult<string>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var launch = await _sessionManager.LaunchAsync(new ApplicationLaunchRequest("patient-demo", [], 5_000), cancellationToken).ConfigureAwait(false);
        if (!launch.Succeeded || launch.Value is null)
        {
            return Failure(launch.ErrorCode ?? ToolErrorCode.ApplicationLaunchFailed, launch.Message ?? "Patient Demo could not be launched.", started);
        }

        try
        {
            var inspection = await _inspector.InspectAsync(
                _sessionManager,
                new UiTreeRequest(launch.Value.SessionId),
                cancellationToken).ConfigureAwait(false);
            if (!inspection.Succeeded || inspection.Value is null)
            {
                return Failure(inspection.ErrorCode ?? ToolErrorCode.AutomationFailure, inspection.Message ?? "Patient Demo inspection failed.", started);
            }

            var directory = Path.GetFullPath(Path.Combine(_configuration.Evidence.RootDirectory, "ui-trees"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"patient-demo-{launch.Value.SessionId}.json");
            await File.WriteAllTextAsync(path, ContractsJson.Serialize(inspection.Value), cancellationToken).ConfigureAwait(false);
            return Success(path, started);
        }
        finally
        {
            await _sessionManager.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => _sessionManager.DisposeAsync();

    private static ToolResult<string> Success(string path, long startedTimestamp) => new(
        true,
        path,
        null,
        null,
        ElapsedMilliseconds(startedTimestamp),
        Guid.NewGuid().ToString("N"));

    private static ToolResult<string> Failure(ToolErrorCode errorCode, string message, long startedTimestamp) => new(
        false,
        null,
        errorCode,
        message,
        ElapsedMilliseconds(startedTimestamp),
        Guid.NewGuid().ToString("N"));

    private static long ElapsedMilliseconds(long startedTimestamp) =>
        (Stopwatch.GetTimestamp() - startedTimestamp) * 1_000 / Stopwatch.Frequency;
}
