using WpfAiAutomation.Contracts;
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
    await using var harness = new AgentServer.PatientSearchScenarioHarness(configuration ?? throw new InvalidOperationException("Automation configuration is empty."));
    var result = await harness.RunAsync(args.SequenceEqual(["run-patient-search-failure"], StringComparer.Ordinal));
    Console.WriteLine(result.Succeeded ? result.Value : $"{result.ErrorCode}: {result.Message}");
    return;
}

Console.Error.WriteLine("AgentServer composition host is bootstrapped. Use 'inspect-patient-demo', 'run-patient-search', or 'run-patient-search-failure' with config/automation.local.json.");
