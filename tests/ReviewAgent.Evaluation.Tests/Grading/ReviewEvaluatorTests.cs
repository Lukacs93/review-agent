using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Grading;
using ReviewAgent.Evaluation.Services;
using static ReviewAgent.Evaluation.Tests.TestReviews;

namespace ReviewAgent.Evaluation.Tests.Grading;

public sealed class ReviewEvaluatorTests
{
    private const string Commit = "9fceb02d0ae598e95dc970b74767f19372d61af8";

    private static readonly RuleSet _rules = new(
    [
        new Rule(new RuleId("ARCH-01"), "Controllers do not access the database directly", Severity.Error,
            "Classes in Controllers/ may only use application-layer handlers."),
    ]);

    [Fact]
    public async Task Kept_comment_that_finds_the_finding_is_counted()
    {
        var result = await EvaluateAsync(
            ReviewStatus.AwaitingApproval,
            [Comment("ARCH-01", ControllerFile, 13)]);

        Assert.Equal(1d, Number(result, ReviewMetrics.Found));
        Assert.Equal(1d, Number(result, ReviewMetrics.CorrectComments));
        Assert.Equal(ReviewMetrics.Completed, ((StringMetric)result.Metrics[ReviewMetrics.Run]).Value);
    }

    // The developer never sees a rejected comment, so it finds nothing; it shows the verifier's mistake instead.
    [Fact]
    public async Task Rejected_comment_that_found_the_finding_is_the_verifiers_mistake()
    {
        var result = await EvaluateAsync(
            ReviewStatus.AwaitingApproval,
            [Comment("ARCH-01", ControllerFile, 13, rejectionReason: "The line only declares the class.")]);

        Assert.Equal(0d, Number(result, ReviewMetrics.Found));
        Assert.Equal(1d, Number(result, ReviewMetrics.RejectedCorrect));
        Assert.Equal(0d, Number(result, ReviewMetrics.RejectedIncorrect));
    }

    // The developer still gets the finding from the kept comment, so the rejected duplicate is no mistake.
    [Fact]
    public async Task Rejected_comment_whose_finding_a_kept_comment_found_is_no_mistake()
    {
        var result = await EvaluateAsync(
            ReviewStatus.AwaitingApproval,
            [
                Comment("ARCH-01", ControllerFile, 13, rejectionReason: "The same as the other comment."),
                Comment("ARCH-01", ControllerFile, 13),
            ]);

        Assert.Equal(1d, Number(result, ReviewMetrics.Found));
        Assert.Equal(0d, Number(result, ReviewMetrics.RejectedCorrect));
        Assert.Equal(1d, Number(result, ReviewMetrics.RejectedIncorrect));
    }

    [Fact]
    public async Task Failed_run_records_its_failure_and_no_comments()
    {
        var result = await EvaluateAsync(
            ReviewStatus.Failed,
            [Comment("ARCH-01", ControllerFile, 13)],
            failureReason: "The review did not finish within 00:05:00 (Agent:Timeout).",
            failureKind: ReviewFailureKind.Agent);

        var run = (StringMetric)result.Metrics[ReviewMetrics.Run];
        Assert.Equal(ReviewMetrics.Failed, run.Value);
        Assert.Equal("The review did not finish within 00:05:00 (Agent:Timeout).", run.Reason);
        Assert.Equal(nameof(ReviewFailureKind.Agent), run.Metadata![ReviewMetrics.FailureKindKey]);
        Assert.Equal(0d, Number(result, ReviewMetrics.KeptComments));
    }

    [Fact]
    public async Task Run_records_the_commit_and_the_tasks_fingerprint()
    {
        var result = await EvaluateAsync(ReviewStatus.AwaitingApproval, []);

        var metadata = result.Metrics[ReviewMetrics.Run].Metadata!;
        Assert.Equal(Commit, metadata[ReviewMetrics.CommitKey]);
        Assert.Equal("fingerprint", metadata[ReviewMetrics.TaskFingerprintKey]);
    }

    private static async Task<EvaluationResult> EvaluateAsync(
        ReviewStatus status,
        IReadOnlyList<ReviewCommentDetails> comments,
        string? failureReason = null,
        ReviewFailureKind? failureKind = null)
    {
        var task = new EvaluationTask(
            "task-01",
            TaskGroup.Positive,
            "Lukacs93/review-agent-demo",
            Commit,
            "feature/task-01",
            "Add an order status endpoint",
            "Adds GET /orders/{id}/status.",
            Patch: string.Empty,
            [Finding("ARCH-01", ControllerFile, false, 13)])
        {
            Fingerprint = "fingerprint",
        };
        var review = new CompletedReview(Review(status, comments, failureReason, failureKind), []);

        return await new ReviewEvaluator().EvaluateAsync(
            [],
            new ChatResponse(),
            additionalContext: [new ReviewRunContext(task, review, _rules, Commit, TimeSpan.FromSeconds(20))]);
    }

    private static double Number(EvaluationResult result, string name) =>
        ((NumericMetric)result.Metrics[name]).Value ?? double.NaN;
}
