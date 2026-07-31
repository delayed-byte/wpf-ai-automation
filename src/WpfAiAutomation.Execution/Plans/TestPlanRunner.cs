using System.Diagnostics;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.Execution;

public sealed class TestPlanRunner
{
    private readonly IAutomationPlanRuntime _runtime;
    private readonly TestPlanValidator _validator;
    private readonly EvidencePathConfiguration _evidenceConfiguration;

    public TestPlanRunner(
        IAutomationPlanRuntime runtime,
        TestPlanValidator validator,
        EvidencePathConfiguration evidenceConfiguration)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _evidenceConfiguration = evidenceConfiguration ?? throw new ArgumentNullException(nameof(evidenceConfiguration));
    }

    public async Task<ToolResult<PlanExecutionSummary>> ExecuteAsync(
        TestPlan plan,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var validation = _validator.Validate(plan);
        if (!validation.IsValid)
        {
            return Failure(
                ToolErrorCode.InvalidRequest,
                string.Join(" ", validation.Errors.Select(error => $"{error.Field}: {error.Message}")),
                startedTimestamp);
        }

        var resolvedPlan = _validator.ResolveVariables(plan);
        var runId = $"plan-{Guid.NewGuid():N}";
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stepResults = new List<PlanStepResult>(resolvedPlan.Steps.Count);
        await using var recorder = await EvidenceRecorder.CreateAsync(
            _evidenceConfiguration,
            runId,
            resolvedPlan.TestId,
            cancellationToken).ConfigureAwait(false);

        EvidenceRunSummary? evidenceSummary = null;
        ToolErrorCode? failureCode = null;
        string? failureMessage = null;
        var launched = false;

        try
        {
            var launch = await _runtime.LaunchAsync(
                new ApplicationLaunchRequest(resolvedPlan.ApplicationKey),
                cancellationToken).ConfigureAwait(false);
            if (!launch.Succeeded || launch.Value is null)
            {
                failureCode = launch.ErrorCode ?? ToolErrorCode.ApplicationLaunchFailed;
                failureMessage = launch.Message ?? "The configured application could not be launched.";
            }
            else
            {
                launched = true;
                foreach (var step in resolvedPlan.Steps.OrderBy(value => value.StepNumber))
                {
                    var outcome = await ExecuteStepAsync(
                        runId,
                        resolvedPlan.TestId,
                        launch.Value.SessionId,
                        step,
                        cancellationToken).ConfigureAwait(false);

                    if (!outcome.Succeeded && _evidenceConfiguration.CaptureScreenshotOnFailure)
                    {
                        outcome = await AddFailureScreenshotAsync(
                            runId,
                            resolvedPlan.TestId,
                            launch.Value.SessionId,
                            step.StepNumber,
                            outcome,
                            cancellationToken).ConfigureAwait(false);
                    }

                    stepResults.Add(outcome.Result);
                    await recorder.AppendAsync(CreateEvidenceEvent(runId, resolvedPlan.TestId, step, outcome), cancellationToken).ConfigureAwait(false);

                    if (!outcome.Succeeded)
                    {
                        failureCode = outcome.Result.ErrorCode ?? ToolErrorCode.AutomationFailure;
                        failureMessage = outcome.Result.Message ?? "Plan execution failed.";
                        await RecordSkippedStepsAsync(recorder, runId, resolvedPlan, step.StepNumber, stepResults, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }
            }
        }
        finally
        {
            if (launched)
            {
                await _runtime.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }

            evidenceSummary = await recorder.FinalizeAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        var summary = new PlanExecutionSummary(
            runId,
            resolvedPlan.TestId,
            startedAtUtc,
            completedAtUtc,
            stepResults.Count(result => result.Result == EvidenceResult.Passed),
            stepResults.Count(result => result.Result == EvidenceResult.Failed),
            stepResults.Count(result => result.Result == EvidenceResult.Skipped),
            stepResults,
            evidenceSummary);

        return failureCode is null
            ? Success(summary, startedTimestamp)
            : Failure(failureCode.Value, failureMessage!, startedTimestamp, summary);
    }

    private async Task<StepOutcome> ExecuteStepAsync(
        string runId,
        string testId,
        string sessionId,
        TestStep step,
        CancellationToken cancellationToken)
    {
        return step switch
        {
            SetTextStep value => FromAction(step, await _runtime.SetTextAsync(
                new SetTextRequest(sessionId, WithTimeout(value.Target, value.TimeoutMilliseconds), value.Value), cancellationToken).ConfigureAwait(false)),
            InvokeStep value => FromAction(step, await _runtime.InvokeAsync(
                new InvokeRequest(sessionId, WithTimeout(value.Target, value.TimeoutMilliseconds)), cancellationToken).ConfigureAwait(false)),
            SelectItemStep value => FromAction(step, await _runtime.SelectItemAsync(
                new SelectItemRequest(sessionId, WithTimeout(value.Target, value.TimeoutMilliseconds), value.ItemAutomationId), cancellationToken).ConfigureAwait(false)),
            ReadElementStep value => FromObservation(step, await _runtime.ReadAsync(
                new ReadElementRequest(sessionId, WithTimeout(value.Target, value.TimeoutMilliseconds), value.Property), cancellationToken).ConfigureAwait(false)),
            WaitForStep value => FromObservation(step, await _runtime.WaitAsync(
                new WaitForRequest(sessionId, value.Target, value.Condition), cancellationToken).ConfigureAwait(false)),
            AssertStep value => FromAssertion(step, await _runtime.AssertAsync(
                new AssertionRequest(sessionId, WithTimeout(value.Target, value.TimeoutMilliseconds), value.Property, value.Operator, value.ExpectedValue, value.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false)),
            CaptureScreenshotStep => FromScreenshot(step, await _runtime.CaptureScreenshotAsync(
                new CaptureScreenshotRequest(sessionId, runId, testId, step.StepNumber, Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false)),
            _ => throw new InvalidOperationException("The validated plan contains an unsupported step."),
        };
    }

    private async Task<StepOutcome> AddFailureScreenshotAsync(
        string runId,
        string testId,
        string sessionId,
        int stepNumber,
        StepOutcome outcome,
        CancellationToken cancellationToken)
    {
        var screenshot = await _runtime.CaptureScreenshotAsync(
            new CaptureScreenshotRequest(sessionId, runId, testId, stepNumber, outcome.Result.CorrelationId),
            cancellationToken).ConfigureAwait(false);
        if (!screenshot.Succeeded || screenshot.Value is null)
        {
            return outcome;
        }

        var artifacts = (outcome.Result.Evidence ?? Array.Empty<EvidenceReference>()).Append(screenshot.Value).ToArray();
        return outcome with { Result = outcome.Result with { Evidence = artifacts } };
    }

    private static async Task RecordSkippedStepsAsync(
        EvidenceRecorder recorder,
        string runId,
        TestPlan plan,
        int failedStepNumber,
        List<PlanStepResult> results,
        CancellationToken cancellationToken)
    {
        foreach (var skipped in plan.Steps.Where(step => step.StepNumber > failedStepNumber).OrderBy(step => step.StepNumber))
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var result = new PlanStepResult(
                skipped.StepNumber,
                ToAction(skipped),
                EvidenceResult.Skipped,
                null,
                "Skipped because a previous step failed.",
                0,
                correlationId);
            results.Add(result);
            var now = DateTimeOffset.UtcNow;
            await recorder.AppendAsync(new EvidenceEvent(
                runId, plan.TestId, skipped.StepNumber, result.Action, null, null, null,
                EvidenceResult.Skipped, null, now, now, 0, CorrelationId: correlationId), cancellationToken).ConfigureAwait(false);
        }
    }

    private static EvidenceEvent CreateEvidenceEvent(string runId, string testId, TestStep step, StepOutcome outcome)
    {
        var completed = DateTimeOffset.UtcNow;
        return new EvidenceEvent(
            runId,
            testId,
            step.StepNumber,
            outcome.Result.Action,
            outcome.Resolved,
            outcome.Before,
            outcome.After,
            outcome.Result.Result,
            outcome.Result.ErrorCode,
            completed - TimeSpan.FromMilliseconds(outcome.Result.DurationMilliseconds),
            completed,
            outcome.Result.DurationMilliseconds,
            outcome.Result.Evidence,
            outcome.Result.CorrelationId);
    }

    private static StepOutcome FromAction(TestStep step, ToolResult<ActionExecutionResult> result) => new(
        result.Succeeded,
        CreateStepResult(step, result),
        result.Value?.After ?? result.Value?.Before,
        result.Value?.Before,
        result.Value?.After);

    private static StepOutcome FromObservation(TestStep step, ToolResult<ObservationResult> result) => new(
        result.Succeeded,
        CreateStepResult(step, result),
        result.Value?.Element,
        result.Value?.Element,
        result.Value?.Element);

    private static StepOutcome FromAssertion(TestStep step, ToolResult<AssertionResult> result) => new(
        result.Succeeded,
        CreateStepResult(step, result),
        result.Value?.Element,
        result.Value?.Element,
        result.Value?.Element);

    private static StepOutcome FromScreenshot(TestStep step, ToolResult<EvidenceReference> result) => new(
        result.Succeeded,
        CreateStepResult(step, result, result.Value is null ? null : [result.Value]),
        null,
        null,
        null);

    private static PlanStepResult CreateStepResult<T>(
        TestStep step,
        ToolResult<T> result,
        IReadOnlyList<EvidenceReference>? evidence = null) => new(
        step.StepNumber,
        ToAction(step),
        result.Succeeded ? EvidenceResult.Passed : EvidenceResult.Failed,
        result.ErrorCode,
        result.Message,
        result.DurationMilliseconds,
        result.CorrelationId,
        evidence ?? result.Evidence);

    private static AutomationAction ToAction(TestStep step) => step switch
    {
        SetTextStep => AutomationAction.SetText,
        InvokeStep => AutomationAction.Invoke,
        SelectItemStep => AutomationAction.SelectItem,
        ReadElementStep => AutomationAction.ReadElement,
        WaitForStep => AutomationAction.WaitFor,
        AssertStep => AutomationAction.Assert,
        CaptureScreenshotStep => AutomationAction.CaptureScreenshot,
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    private static ElementLocator WithTimeout(ElementLocator target, int timeoutMilliseconds) =>
        target with { TimeoutMilliseconds = timeoutMilliseconds };

    private static ToolResult<PlanExecutionSummary> Success(PlanExecutionSummary summary, long startedTimestamp) => new(
        true, summary, null, null, ElapsedMilliseconds(startedTimestamp), Guid.NewGuid().ToString("N"));

    private static ToolResult<PlanExecutionSummary> Failure(
        ToolErrorCode errorCode,
        string message,
        long startedTimestamp,
        PlanExecutionSummary? summary = null) => new(
        false, summary, errorCode, message, ElapsedMilliseconds(startedTimestamp), Guid.NewGuid().ToString("N"));

    private static long ElapsedMilliseconds(long startedTimestamp) =>
        (Stopwatch.GetTimestamp() - startedTimestamp) * 1_000 / Stopwatch.Frequency;

    private sealed record StepOutcome(
        bool Succeeded,
        PlanStepResult Result,
        ElementSnapshot? Resolved,
        ElementSnapshot? Before,
        ElementSnapshot? After);
}
