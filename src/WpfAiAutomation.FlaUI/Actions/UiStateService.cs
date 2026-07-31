using FlaUI.Core.AutomationElements;
using WpfAiAutomation.Contracts;
using System.Diagnostics;

namespace WpfAiAutomation.FlaUI;

public sealed class UiStateService
{
    private readonly UiInspector _inspector;
    private readonly DeterministicAutomationFaultInjector _faultInjector;

    public UiStateService(UiInspector inspector, DeterministicAutomationFaultInjector? faultInjector = null)
    {
        _inspector = inspector;
        _faultInjector = faultInjector ?? new DeterministicAutomationFaultInjector();
    }

    public Task<ToolResult<ObservationResult>> ReadAsync(
        AutomationSessionManager sessionManager,
        ReadElementRequest request,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            VerifySession(session, request.SessionId);
            return Observe(session, request.Target, request.Property);
        }, cancellationToken);
    }

    public Task<ToolResult<ObservationResult>> WaitAsync(
        AutomationSessionManager sessionManager,
        string sessionId,
        ElementLocator target,
        WaitCondition condition,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(async (session, token) =>
        {
            VerifySession(session, sessionId);
            _faultInjector.ThrowIfConfigured(AutomationFaultPoint.WaitForCondition);
            var started = Stopwatch.GetTimestamp();
            ObservationResult? latest = null;

            while (ElapsedMilliseconds(started) < condition.TimeoutMilliseconds)
            {
                token.ThrowIfCancellationRequested();
                latest = Observe(session, target, condition.Property);
                if (Matches(latest, condition.Operator, condition.ExpectedValue))
                {
                    return latest;
                }

                await Task.Delay(condition.PollIntervalMilliseconds, token).ConfigureAwait(false);
            }

            throw new AutomationOperationException(
                ToolErrorCode.ConditionTimeout,
                "The expected UI condition was not met before the timeout.",
                latest);
        }, cancellationToken);
    }

    public Task<ToolResult<AssertionResult>> AssertAsync(
        AutomationSessionManager sessionManager,
        AssertionRequest request,
        CancellationToken cancellationToken = default)
    {
        return sessionManager.ExecuteSerializedAsync(session =>
        {
            VerifySession(session, request.SessionId);
            var observation = Observe(session, request.Target, request.Property);
            var passed = Matches(observation, request.Operator, request.ExpectedValue);
            var result = new AssertionResult(
                passed,
                request.Property,
                request.Operator,
                request.ExpectedValue,
                observation.Value,
                observation.Element);

            if (!passed)
            {
                throw new AutomationOperationException(
                    ToolErrorCode.AssertionFailed,
                    "The UI assertion did not match the observed state.",
                    result);
            }

            return result;
        }, cancellationToken);
    }

    internal static bool Matches(ObservationResult observation, ComparisonOperator @operator, string? expectedValue)
    {
        return @operator switch
        {
            ComparisonOperator.Exists => observation.Exists,
            ComparisonOperator.NotExists => !observation.Exists,
            ComparisonOperator.NotEmpty => !string.IsNullOrWhiteSpace(observation.Value),
            ComparisonOperator.Enabled => string.Equals(observation.Value, bool.TrueString, StringComparison.Ordinal),
            ComparisonOperator.Visible => string.Equals(observation.Value, bool.TrueString, StringComparison.Ordinal),
            ComparisonOperator.Equals => string.Equals(observation.Value, expectedValue, StringComparison.Ordinal),
            ComparisonOperator.NotEquals => !string.Equals(observation.Value, expectedValue, StringComparison.Ordinal),
            ComparisonOperator.Contains => observation.Value?.Contains(expectedValue ?? string.Empty, StringComparison.Ordinal) == true,
            _ => false,
        };
    }

    private ObservationResult Observe(AutomationSession session, ElementLocator target, AutomationProperty property)
    {
        _faultInjector.ThrowIfConfigured(AutomationFaultPoint.ResolveElement);
        var resolution = ElementResolver.Resolve(session, target);
        if (resolution.Element is null)
        {
            if (resolution.ErrorCode == ToolErrorCode.ElementNotFound && property == AutomationProperty.Exists)
            {
                return new ObservationResult(null, property, bool.FalseString, Exists: false);
            }

            throw new AutomationOperationException(
                resolution.ErrorCode ?? ToolErrorCode.AutomationFailure,
                resolution.Message ?? "Element resolution failed.");
        }

        var element = resolution.Element;
        var snapshot = _inspector.Snapshot(element, includeChildren: false);
        var value = property switch
        {
            AutomationProperty.Exists => bool.TrueString,
            AutomationProperty.Name => snapshot.Name,
            AutomationProperty.Value => snapshot.Value,
            AutomationProperty.Enabled => snapshot.IsEnabled.ToString(),
            AutomationProperty.Visible => snapshot.IsVisible.ToString(),
            _ => throw new AutomationOperationException(ToolErrorCode.InvalidRequest, "The requested property is not supported."),
        };

        return new ObservationResult(snapshot, property, value, Exists: true);
    }

    private static void VerifySession(AutomationSession session, string sessionId)
    {
        if (!string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new AutomationOperationException(ToolErrorCode.NoActiveSession, "The request does not reference the active session.");
        }
    }

    private static long ElapsedMilliseconds(long startedTimestamp) =>
        (Stopwatch.GetTimestamp() - startedTimestamp) * 1_000 / Stopwatch.Frequency;
}
