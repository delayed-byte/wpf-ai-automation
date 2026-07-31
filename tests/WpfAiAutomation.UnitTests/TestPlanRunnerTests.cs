using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;

namespace WpfAiAutomation.UnitTests;

public sealed class TestPlanRunnerTests
{
    [Fact]
    public async Task InvalidPlanIsRejectedBeforeLaunchOrMutation()
    {
        using var artifacts = new TemporaryDirectory();
        var runtime = new RecordingRuntime();
        var runner = CreateRunner(runtime, artifacts.Path);
        var plan = Plan(new SetTextStep(1, new ElementLocator(string.Empty), "value"));

        var result = await runner.ExecuteAsync(plan);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolErrorCode.InvalidRequest, result.ErrorCode);
        Assert.Equal(0, runtime.LaunchCalls);
        Assert.Equal(0, runtime.MutationCalls);
        Assert.False(Directory.Exists(artifacts.ArtifactRoot));
    }

    [Fact]
    public async Task ExecutesResolvedStepsSequentiallyAndAlwaysCloses()
    {
        using var artifacts = new TemporaryDirectory();
        var runtime = new RecordingRuntime();
        var runner = CreateRunner(runtime, artifacts.Path);
        var plan = Plan(
            new SetTextStep(1, Locator(), "${patientId}"),
            new AssertStep(2, Locator("StatusMessage"), AutomationProperty.Name, ComparisonOperator.Equals, "ready"));

        var result = await runner.ExecuteAsync(plan);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(["launch", "setText:PM-1001", "assert", "close"], runtime.Calls);
        Assert.Equal(2, result.Value!.PassedSteps);
        Assert.Equal(0, result.Value.FailedSteps);
        Assert.True(File.Exists(System.IO.Path.Combine(artifacts.ArtifactRoot, result.Value.RunId, "run.json")));
    }

    [Fact]
    public async Task StopsAtFirstFailureAndMarksRemainingStepsSkipped()
    {
        using var artifacts = new TemporaryDirectory();
        var runtime = new RecordingRuntime { FailInvoke = true };
        var runner = CreateRunner(runtime, artifacts.Path);
        var plan = Plan(
            new SetTextStep(1, Locator(), "PM-1001"),
            new InvokeStep(2, Locator("SearchButton")),
            new AssertStep(3, Locator("StatusMessage"), AutomationProperty.Name, ComparisonOperator.Equals, "ready"));

        var result = await runner.ExecuteAsync(plan);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolErrorCode.ElementDisabled, result.ErrorCode);
        Assert.Equal(["launch", "setText:PM-1001", "invoke", "screenshot", "close"], runtime.Calls);
        Assert.Equal(1, result.Value!.FailedSteps);
        Assert.Equal(1, result.Value.SkippedSteps);
        Assert.Equal(3, File.ReadAllLines(System.IO.Path.Combine(artifacts.ArtifactRoot, result.Value.RunId, "events.jsonl")).Length);
    }

    [Fact]
    public async Task CancellationStillClosesTheLaunchedSessionAndFinalizesEvidence()
    {
        using var artifacts = new TemporaryDirectory();
        var runtime = new RecordingRuntime { CancelSetText = true };
        var runner = CreateRunner(runtime, artifacts.Path);

        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.ExecuteAsync(
            Plan(new SetTextStep(1, Locator(), "PM-1001"))));

        Assert.Equal(["launch", "setText:PM-1001", "close"], runtime.Calls);
        var runDirectory = Assert.Single(Directory.GetDirectories(artifacts.ArtifactRoot));
        Assert.True(File.Exists(System.IO.Path.Combine(runDirectory, "run.json")));
    }

    private static TestPlanRunner CreateRunner(RecordingRuntime runtime, string root) => new(
        runtime,
        new TestPlanValidator(PolicyCeilings.Default),
        new EvidencePathConfiguration(System.IO.Path.Combine(root, "artifacts")));

    private static TestPlan Plan(params TestStep[] steps) => new(
        "1.0",
        "patient-search",
        "patient-demo",
        new Dictionary<string, string> { ["patientId"] = "PM-1001" },
        steps);

    private static ElementLocator Locator(string automationId = "PatientIdTextBox") => new(automationId);

    private sealed class RecordingRuntime : IAutomationPlanRuntime
    {
        private static readonly ElementSnapshot Snapshot = new(
            "element", "Element", AutomationControlType.Text, true, true, [], "ready");

        public List<string> Calls { get; } = [];

        public int LaunchCalls { get; private set; }

        public int MutationCalls { get; private set; }

        public bool FailInvoke { get; init; }

        public bool CancelSetText { get; init; }

        public Task<ToolResult<AutomationSessionInfo>> LaunchAsync(ApplicationLaunchRequest request, CancellationToken cancellationToken = default)
        {
            LaunchCalls++;
            Calls.Add("launch");
            return Result(new AutomationSessionInfo("session", 42, "1.0", Snapshot, DateTimeOffset.UtcNow));
        }

        public Task<ToolResult<bool>> CloseAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("close");
            return Result(true);
        }

        public Task<ToolResult<ActionExecutionResult>> SetTextAsync(SetTextRequest request, CancellationToken cancellationToken = default)
        {
            MutationCalls++;
            Calls.Add($"setText:{request.Value}");
            if (CancelSetText)
            {
                throw new OperationCanceledException();
            }

            return Result(new ActionExecutionResult(Snapshot, Snapshot));
        }

        public Task<ToolResult<ActionExecutionResult>> InvokeAsync(InvokeRequest request, CancellationToken cancellationToken = default)
        {
            MutationCalls++;
            Calls.Add("invoke");
            return FailInvoke
                ? Failure<ActionExecutionResult>(ToolErrorCode.ElementDisabled, "disabled")
                : Result(new ActionExecutionResult(Snapshot, Snapshot));
        }

        public Task<ToolResult<ActionExecutionResult>> SelectItemAsync(SelectItemRequest request, CancellationToken cancellationToken = default) =>
            Result(new ActionExecutionResult(Snapshot, Snapshot));

        public Task<ToolResult<ObservationResult>> ReadAsync(ReadElementRequest request, CancellationToken cancellationToken = default) =>
            Result(new ObservationResult(Snapshot, request.Property, "ready", true));

        public Task<ToolResult<ObservationResult>> WaitAsync(WaitForRequest request, CancellationToken cancellationToken = default) =>
            Result(new ObservationResult(Snapshot, request.Condition.Property, "ready", true));

        public Task<ToolResult<AssertionResult>> AssertAsync(AssertionRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add("assert");
            return Result(new AssertionResult(true, request.Property, request.Operator, request.ExpectedValue, "ready", Snapshot));
        }

        public Task<ToolResult<EvidenceReference>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add("screenshot");
            return Result(new EvidenceReference("screenshots/failure.png", "screenshot"));
        }

        private static Task<ToolResult<T>> Result<T>(T value) => Task.FromResult(new ToolResult<T>(
            true, value, null, null, 1, Guid.NewGuid().ToString("N")));

        private static Task<ToolResult<T>> Failure<T>(ToolErrorCode code, string message) => Task.FromResult(new ToolResult<T>(
            false, default, code, message, 1, Guid.NewGuid().ToString("N")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wpf-ai-automation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string ArtifactRoot => System.IO.Path.Combine(Path, "artifacts");

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
