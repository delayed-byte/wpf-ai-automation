using WpfAiAutomation.Contracts;
using WpfAiAutomation.FlaUI;

namespace WpfAiAutomation.PatientDemoTests.PageObjects;

public sealed class PatientSearchPage
{
    private static readonly ElementLocator PatientId = new("PatientIdTextBox", AutomationControlType.Edit);
    private static readonly ElementLocator SearchButton = new("SearchButton", AutomationControlType.Button);
    private static readonly ElementLocator StatusMessage = new("StatusMessage", AutomationControlType.Text);

    private readonly AutomationSessionManager _sessionManager;
    private readonly UiActionService _actions;
    private readonly UiStateService _state;
    private readonly string _sessionId;

    public Action<PatientSearchOperation>? OperationObserver { get; set; }

    public PatientSearchPage(
        AutomationSessionManager sessionManager,
        UiActionService actions,
        UiStateService state,
        string sessionId)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _sessionId = sessionId;
    }

    public async Task EnterPatientIdAsync(string patientId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patientId);
        var result = await _actions.SetTextAsync(
            _sessionManager,
            new SetTextRequest(_sessionId, PatientId, patientId),
            cancellationToken).ConfigureAwait(false);

        Report(AutomationAction.SetText, PatientId, result, result.Value?.After, result.Value?.Before, result.Value?.After);
        EnsureSucceeded(result, "Patient ID entry failed.");
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var result = await _actions.InvokeAsync(
            _sessionManager,
            new InvokeRequest(_sessionId, SearchButton),
            cancellationToken).ConfigureAwait(false);

        Report(AutomationAction.Invoke, SearchButton, result, result.Value?.After, result.Value?.Before, result.Value?.After);
        EnsureSucceeded(result, "Patient search could not be invoked.");
    }

    public async Task<string> WaitForSearchResultAsync(
        string expectedStatus,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedStatus);
        var timeoutMilliseconds = checked((int)timeout.TotalMilliseconds);
        if (timeoutMilliseconds is < 1 or > 60_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be between 1 millisecond and 1 minute.");
        }

        var result = await _state.WaitAsync(
            _sessionManager,
            _sessionId,
            StatusMessage,
            new WaitCondition(
                AutomationProperty.Name,
                ComparisonOperator.Equals,
                expectedStatus,
                PollIntervalMilliseconds: Math.Min(100, timeoutMilliseconds),
                TimeoutMilliseconds: timeoutMilliseconds),
            cancellationToken).ConfigureAwait(false);

        Report(AutomationAction.WaitFor, StatusMessage, result, result.Value?.Element, result.Value?.Element, result.Value?.Element);
        EnsureSucceeded(result, "The patient search result did not become ready.");
        return result.Value!.Value ?? string.Empty;
    }

    private void Report<T>(
        AutomationAction action,
        ElementLocator target,
        ToolResult<T> result,
        ElementSnapshot? resolvedElement,
        ElementSnapshot? before,
        ElementSnapshot? after) =>
        OperationObserver?.Invoke(new PatientSearchOperation(
            action,
            target,
            resolvedElement,
            before,
            after,
            result.Succeeded,
            result.ErrorCode,
            result.DurationMilliseconds,
            result.CorrelationId));

    private static void EnsureSucceeded<T>(ToolResult<T> result, string fallbackMessage)
    {
        if (!result.Succeeded)
        {
            throw new PatientSearchPageException(
                result.ErrorCode ?? ToolErrorCode.AutomationFailure,
                result.Message ?? fallbackMessage);
        }
    }
}

public sealed record PatientSearchOperation(
    AutomationAction Action,
    ElementLocator Target,
    ElementSnapshot? ResolvedElement,
    ElementSnapshot? Before,
    ElementSnapshot? After,
    bool Succeeded,
    ToolErrorCode? ErrorCode,
    long DurationMilliseconds,
    string CorrelationId);

public sealed class PatientSearchPageException(ToolErrorCode errorCode, string message) : Exception(message)
{
    public ToolErrorCode ErrorCode { get; } = errorCode;
}
