using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.FlaUI;

public sealed class AutomationSessionManager : IAsyncDisposable
{
    private readonly ApplicationCatalog _catalog;
    private readonly UiInspector _inspector;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private AutomationSession? _session;

    public AutomationSessionManager(ApplicationCatalog catalog, UiInspector inspector)
    {
        _catalog = catalog;
        _inspector = inspector;
    }

    public async Task<ToolResult<AutomationSessionInfo>> LaunchAsync(ApplicationLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_session is not null)
            {
                return Failure<AutomationSessionInfo>(ToolErrorCode.InvalidRequest, "An automation session is already active.", started);
            }

            if (!_catalog.TryResolve(request, out var configuredApplication, out var errorCode, out var message))
            {
                return Failure<AutomationSessionInfo>(errorCode, message, started);
            }

            var applicationDefinition = configuredApplication!;
            Application? application = null;
            UIA3Automation? automation = null;
            try
            {
                application = Application.Launch(
                    applicationDefinition.ExecutablePath,
                    string.Join(' ', request.Arguments ?? Array.Empty<string>()));
                automation = new UIA3Automation();

                var mainWindow = await WaitForMainWindowAsync(application, automation, request.StartupTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                if (mainWindow is null)
                {
                    automation.Dispose();
                    application.Close(applicationDefinition.AllowForcedTermination);
                    application.Dispose();
                    return Failure<AutomationSessionInfo>(ToolErrorCode.WindowTimeout, "The application main window did not appear before the startup timeout.", started);
                }

                var session = new AutomationSession(application, automation, mainWindow, applicationDefinition);
                session.TransitionTo(AutomationSessionState.Ready);
                _session = session;

                var info = new AutomationSessionInfo(
                    session.SessionId,
                    session.ProcessId,
                    TryGetVersion(applicationDefinition.ExecutablePath),
                    _inspector.Snapshot(mainWindow, includeChildren: false),
                    session.StartedAtUtc);

                return Success(info, started);
            }
            catch (OperationCanceledException)
            {
                automation?.Dispose();
                application?.Dispose();
                throw;
            }
            catch (Exception)
            {
                automation?.Dispose();
                application?.Dispose();
                return Failure<AutomationSessionInfo>(ToolErrorCode.ApplicationLaunchFailed, "The configured application could not be launched.", started);
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<ToolResult<bool>> CloseAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_session is not { State: AutomationSessionState.Ready } session)
            {
                return Failure<bool>(ToolErrorCode.NoActiveSession, "There is no active automation session.", started);
            }

            session.TransitionTo(AutomationSessionState.Closing);
            await session.OperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                session.Application.Close(killIfCloseFails: false);
                var closed = await WaitForExitAsync(session.Application, session.ConfiguredApplication.CloseTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);

                if (!closed && session.ConfiguredApplication.AllowForcedTermination)
                {
                    session.Application.Close(killIfCloseFails: true);
                    closed = await WaitForExitAsync(session.Application, session.ConfiguredApplication.CloseTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                }

                if (!closed)
                {
                    session.TransitionTo(AutomationSessionState.Faulted);
                    return Failure<bool>(ToolErrorCode.AutomationFailure, "The application did not close before the configured timeout.", started);
                }

                session.TransitionTo(AutomationSessionState.Closed);
                session.Dispose();
                _session = null;
                return Success(true, started);
            }
            finally
            {
                if (session.State != AutomationSessionState.Closed)
                {
                    session.OperationLock.Release();
                }
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    internal async Task<ToolResult<T>> ExecuteSerializedAsync<T>(Func<AutomationSession, T> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var started = Stopwatch.GetTimestamp();
        var session = _session;
        if (session is not { State: AutomationSessionState.Ready })
        {
            return Failure<T>(ToolErrorCode.NoActiveSession, "There is no active automation session.", started);
        }

        await session.OperationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Success(operation(session), started);
        }
        catch (AutomationOperationException exception)
        {
            return Failure<T>(exception.ErrorCode, exception.Message, started);
        }
        catch (Exception)
        {
            return Failure<T>(ToolErrorCode.AutomationFailure, "The UI Automation operation failed.", started);
        }
        finally
        {
            session.OperationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }

    private static async Task<Window?> WaitForMainWindowAsync(Application application, UIA3Automation automation, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * timeoutMilliseconds / 1_000);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mainWindow = application.GetMainWindow(automation, TimeSpan.FromMilliseconds(100));
            if (mainWindow is not null)
            {
                return mainWindow;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<bool> WaitForExitAsync(Application application, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * timeoutMilliseconds / 1_000);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (application.HasExited)
            {
                return true;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return application.HasExited;
    }

    private static string? TryGetVersion(string executablePath)
    {
        return File.Exists(executablePath) ? FileVersionInfo.GetVersionInfo(executablePath).FileVersion : null;
    }

    private static ToolResult<T> Success<T>(T value, long startedTimestamp) => new(
        true,
        value,
        null,
        null,
        ElapsedMilliseconds(startedTimestamp),
        Guid.NewGuid().ToString("N"));

    private static ToolResult<T> Failure<T>(ToolErrorCode errorCode, string message, long startedTimestamp) => new(
        false,
        default,
        errorCode,
        message,
        ElapsedMilliseconds(startedTimestamp),
        Guid.NewGuid().ToString("N"));

    private static long ElapsedMilliseconds(long startedTimestamp) =>
        (Stopwatch.GetTimestamp() - startedTimestamp) * 1_000 / Stopwatch.Frequency;
}
