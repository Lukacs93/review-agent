using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Grading;
using ReviewAgent.Evaluation.Measuring;
using ReviewAgent.Evaluation.Summary;
using static ReviewAgent.Evaluation.Tests.TestReviews;

namespace ReviewAgent.Evaluation.Tests.Summary;

public sealed class EvaluationSummaryTests
{
    private const int RunsPerTask = 2;

    private static readonly ExecutionRecord _execution = new(
        "baseline",
        new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero),
        "9fceb02",
        RunsPerTask,
        new AgentSettings(
            new Uri("https://model.example/"),
            "test-model",
            VerifierEnabled: true,
            MaxToolRounds: 40,
            RequestTimeout: TimeSpan.FromMinutes(2),
            Timeout: TimeSpan.FromMinutes(5),
            RequestsPerMinute: 15));

    private static readonly EvaluationTask _positive = new(
        "task-01",
        TaskGroup.Positive,
        "owner/demo",
        "abc",
        "feature/task-01",
        "Add an endpoint",
        "Adds an endpoint.",
        Patch: string.Empty,
        [Finding("ARCH-01", ControllerFile, false, 13)]);

    [Fact]
    public void Coverage_counts_the_completed_runs()
    {
        var summary = Summarize(Completed(found: true), Completed(found: false));

        Assert.Equal<double?>(0.5, summary.Coverage);
        Assert.Equal<double?>(0, summary.Consistency);
    }

    // The developer gets nothing from a failed run, so it is not a found finding in every run.
    [Fact]
    public void Failed_run_counts_apart_and_as_a_miss_in_the_consistency()
    {
        var summary = Summarize(Completed(found: true), Failed());

        Assert.Equal<double?>(1, summary.Coverage);
        Assert.Equal<double?>(0, summary.Consistency);
        Assert.Equal(1, summary.CompletedRuns);
        Assert.Equal("task-01", Assert.Single(summary.FailedRuns).TaskId);
    }

    [Fact]
    public void Finding_found_in_every_run_is_consistent()
    {
        var summary = Summarize(Completed(found: true), Completed(found: true));

        Assert.Equal<double?>(1, summary.Consistency);
        Assert.Equal(2, Assert.Single(Assert.Single(summary.Tasks).Findings).FoundIn);
    }

    private static EvaluationSummary Summarize(params RunResult[] runs) =>
        EvaluationSummary.From(
            _execution,
            [_positive],
            new Dictionary<string, IReadOnlyList<RunResult>> { [_positive.Id] = runs });

    private static RunResult Completed(bool found) =>
        new(
            _positive.Id,
            Completed: true,
            FailureReason: null,
            FailureKind: null,
            Commit: "9fceb02",
            [found],
            new Dictionary<string, int>(),
            new Dictionary<string, double?>
            {
                [ReviewMetrics.Found] = found ? 1 : 0,
                [ReviewMetrics.Required] = 1,
                [ReviewMetrics.KeptComments] = found ? 1 : 0,
                [ReviewMetrics.CorrectComments] = found ? 1 : 0,
            });

    private static RunResult Failed() =>
        new(
            _positive.Id,
            Completed: false,
            FailureReason: "The review did not finish within 00:05:00 (Agent:Timeout).",
            FailureKind: "Agent",
            Commit: "9fceb02",
            [false],
            new Dictionary<string, int>(),
            new Dictionary<string, double?>());
}
