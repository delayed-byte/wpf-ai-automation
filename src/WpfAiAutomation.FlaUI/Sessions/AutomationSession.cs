using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace WpfAiAutomation.FlaUI;

public sealed class AutomationSession : IDisposable
{
    private bool _disposed;

    internal AutomationSession(Application application, UIA3Automation automation, Window mainWindow, ApprovedApplication configuredApplication)
    {
        Application = application;
        Automation = automation;
        MainWindow = mainWindow;
        ConfiguredApplication = configuredApplication;
        SessionId = Guid.NewGuid().ToString("N");
        StartedAtUtc = DateTimeOffset.UtcNow;
        State = AutomationSessionState.Starting;
    }

    public string SessionId { get; }

    public int ProcessId => Application.ProcessId;

    public DateTimeOffset StartedAtUtc { get; }

    public AutomationSessionState State { get; private set; }

    internal Application Application { get; }

    internal UIA3Automation Automation { get; }

    internal Window MainWindow { get; }

    internal ApprovedApplication ConfiguredApplication { get; }

    internal SemaphoreSlim OperationLock { get; } = new(1, 1);

    internal void TransitionTo(AutomationSessionState state) => State = state;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Automation.Dispose();
        Application.Dispose();
        OperationLock.Dispose();
    }
}
