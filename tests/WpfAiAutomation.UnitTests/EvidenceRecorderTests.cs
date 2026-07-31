using WpfAiAutomation.Contracts;
using WpfAiAutomation.Execution;

namespace WpfAiAutomation.UnitTests;

public sealed class EvidenceRecorderTests
{
    [Fact]
    public async Task RecorderWritesFlushedJsonLinesAndFinalRunSummary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wpf-ai-automation-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var recorder = await EvidenceRecorder.CreateAsync(
                new EvidencePathConfiguration(directory),
                "run-001",
                "test-001");

            await recorder.AppendAsync(CreateEvent("run-001", "test-001", EvidenceResult.Passed));
            var summary = await recorder.FinalizeAsync();

            var eventsPath = Path.Combine(directory, "run-001", "events.jsonl");
            var summaryPath = Path.Combine(directory, "run-001", "run.json");
            Assert.True(File.Exists(eventsPath));
            Assert.True(File.Exists(summaryPath));
            Assert.Contains("\"correlationId\": \"corr-001\"", await File.ReadAllTextAsync(eventsPath), StringComparison.Ordinal);
            Assert.Equal(1, summary.PassedSteps);
            Assert.Equal(0, summary.FailedSteps);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecorderRejectsEvidenceFromAnotherRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wpf-ai-automation-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var recorder = await EvidenceRecorder.CreateAsync(
                new EvidencePathConfiguration(directory),
                "run-001",
                "test-001");

            await Assert.ThrowsAsync<ArgumentException>(() => recorder.AppendAsync(CreateEvent("run-002", "test-001", EvidenceResult.Failed)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static EvidenceEvent CreateEvent(string runId, string testId, EvidenceResult result)
    {
        var started = DateTimeOffset.UtcNow;
        return new EvidenceEvent(
            runId,
            testId,
            1,
            AutomationAction.Assert,
            null,
            null,
            null,
            result,
            result == EvidenceResult.Failed ? ToolErrorCode.AssertionFailed : null,
            started,
            started,
            0,
            CorrelationId: "corr-001");
    }
}
