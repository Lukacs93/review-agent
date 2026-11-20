using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Tests.Reviews.Queries.GetReviewRun;

public sealed class GetReviewRunQueryHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private GetReviewRunQueryHandler Handler => new(_repository);

    [Fact]
    public async Task Run_is_returned_as_plain_data()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var details = await Handler.HandleAsync(new GetReviewRunQuery(runId), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(runId, details.Id);
        Assert.Equal(TestReviewRuns.RepositoryOwner, details.RepositoryOwner);
        Assert.Equal(TestReviewRuns.RepositoryName, details.RepositoryName);
        Assert.Equal(TestReviewRuns.PullRequestNumber, details.PullRequestNumber);
        Assert.Equal(ReviewStatus.AwaitingApproval, details.Status);
        Assert.Equal(TestReviewRuns.Now, details.StartedAt);
        Assert.Equal(1, details.ToolCallCount);
        Assert.Equal(TestReviewRuns.Summary, details.Summary);
        Assert.Null(details.FailureReason);

        var comment = Assert.Single(details.Comments);
        Assert.Equal(commentId, comment.Id);
        Assert.Equal(TestReviewRuns.FilePath, comment.FilePath);
        Assert.Equal(TestReviewRuns.Line, comment.Line);
        Assert.Equal(TestReviewRuns.Rule, comment.RuleId);
        Assert.Equal(Severity.Error, comment.Severity);
        Assert.Equal(TestReviewRuns.CommentBody, comment.ProposedBody);
        Assert.Equal(TestReviewRuns.CommentBody, comment.Body);
        Assert.Null(comment.SuggestedFix);
        Assert.Equal(CommentDecision.Pending, comment.Decision);
        Assert.False(comment.IsPublished);
        Assert.Null(comment.RejectionReason);
    }

    [Fact]
    public async Task Verifier_summary_counts_the_kept_and_rejected_comments()
    {
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(TestReviewRuns.RepositoryOwner, TestReviewRuns.RepositoryName, 1),
            TestReviewRuns.Now);
        var rejectedId = ProposeComment(run, line: 10);
        ProposeComment(run, line: 11);
        ProposeComment(run, line: 12);
        run.RejectComment(rejectedId, "The line does not show the problem.", TestReviewRuns.Now);
        run.FinishVerification(TestReviewRuns.Now);
        await _repository.SaveAsync(run, CancellationToken.None);

        var details = await Handler.HandleAsync(new GetReviewRunQuery(run.Id.Value), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal("Kept: 2, rejected: 1.", details.VerifierSummary);
    }

    [Fact]
    public async Task Run_without_verification_has_no_verifier_summary()
    {
        var (runId, _) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var details = await Handler.HandleAsync(new GetReviewRunQuery(runId), CancellationToken.None);

        Assert.NotNull(details);
        Assert.Null(details.VerifierSummary);
    }

    [Fact]
    public async Task Unknown_run_returns_null()
    {
        var details = await Handler.HandleAsync(new GetReviewRunQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(details);
    }

    private static CommentId ProposeComment(ReviewRun run, int line) =>
        run.ProposeComment(
            new CodeLocation(TestReviewRuns.FilePath, line),
            new RuleId(TestReviewRuns.Rule),
            Severity.Error,
            TestReviewRuns.CommentBody,
            suggestedFix: null,
            TestReviewRuns.Now);
}
