namespace WpfAiAutomation.PatientDemoTests.Fixtures;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PatientDemoFactAttribute : FactAttribute
{
    public PatientDemoFactAttribute()
    {
        var configurationPath = PatientDemoTestEnvironment.FindConfigurationPath();
        if (configurationPath is null)
        {
            Skip = "Create config/automation.local.json to run Patient Demo regression tests.";
            return;
        }

        try
        {
            _ = PatientDemoTestEnvironment.LoadConfiguration();
        }
        catch (InvalidOperationException exception)
        {
            Skip = exception.Message;
        }
    }
}
