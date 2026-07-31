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

Console.Error.WriteLine("AgentServer composition host is bootstrapped. Use 'inspect-patient-demo' only with config/automation.local.json.");
