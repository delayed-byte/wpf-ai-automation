using AgentServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;
using WpfAiAutomation.FlaUI;

if (args.SequenceEqual(["inspect-patient-demo"], StringComparer.Ordinal))
{
    const string ConfigurationPath = "config/automation.local.json";
    if (!File.Exists(ConfigurationPath))
    {
        Console.Error.WriteLine($"Create {ConfigurationPath} from config/automation.sample.json before running this local inspection harness.");
        return;
    }

    var configuration = ContractsJson.Deserialize<AutomationConfiguration>(await File.ReadAllTextAsync(ConfigurationPath));
    await using var harness = new PatientDemoInspectionHarness(configuration ?? throw new InvalidOperationException("Automation configuration is empty."));
    var result = await harness.CaptureAsync();

    Console.WriteLine(result.Succeeded ? result.Value : $"{result.ErrorCode}: {result.Message}");
    return;
}

if (args.SequenceEqual(["run-patient-search"], StringComparer.Ordinal)
    || args.SequenceEqual(["run-patient-search-failure"], StringComparer.Ordinal))
{
    const string ConfigurationPath = "config/automation.local.json";
    if (!File.Exists(ConfigurationPath))
    {
        Console.Error.WriteLine($"Create {ConfigurationPath} from config/automation.sample.json before running this local scenario harness.");
        return;
    }

    var configuration = ContractsJson.Deserialize<AutomationConfiguration>(await File.ReadAllTextAsync(ConfigurationPath));
    await using var harness = new PatientSearchScenarioHarness(configuration ?? throw new InvalidOperationException("Automation configuration is empty."));
    var result = await harness.RunAsync(args.SequenceEqual(["run-patient-search-failure"], StringComparer.Ordinal));
    Console.WriteLine(result.Succeeded ? result.Value : $"{result.ErrorCode}: {result.Message}");
    return;
}

if (args.SequenceEqual(["mcp"], StringComparer.Ordinal))
{
    const string ConfigurationPath = "config/automation.local.json";
    if (!File.Exists(ConfigurationPath))
    {
        Console.Error.WriteLine($"Create {ConfigurationPath} from config/automation.sample.json before starting the MCP server.");
        return;
    }

    var configuration = ContractsJson.Deserialize<AutomationConfiguration>(await File.ReadAllTextAsync(ConfigurationPath))
        ?? throw new InvalidOperationException("Automation configuration is empty.");
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddSingleton(configuration);
    builder.Services.AddSingleton(configuration.Policy);
    builder.Services.AddSingleton(configuration.Evidence);
    builder.Services.AddSingleton(configuration.SensitiveControls);
    builder.Services.AddSingleton<SensitiveDataRedactor>();
    builder.Services.AddSingleton<UiInspector>();
    builder.Services.AddSingleton<ApplicationCatalog>();
    builder.Services.AddSingleton<AutomationSessionManager>();
    builder.Services.AddSingleton<UiActionService>();
    builder.Services.AddSingleton<UiStateService>();
    builder.Services.AddSingleton<RedactedScreenshotService>();
    builder.Services.AddSingleton<TestPlanValidator>();
    builder.Services.AddSingleton<IAutomationPlanRuntime, FlaUiPlanRuntime>();
    builder.Services.AddSingleton<TestPlanRunner>();
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<AutomationTools>(ContractsJson.Default);

    await builder.Build().RunAsync();
    return;
}

Console.Error.WriteLine("Use 'mcp', 'inspect-patient-demo', 'run-patient-search', or 'run-patient-search-failure' with config/automation.local.json.");
