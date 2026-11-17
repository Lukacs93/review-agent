using ReviewAgent.Application.Reviews.Queries;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Tests.Reviews.Queries;

public sealed class GetReviewRunHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private GetReviewRunHandler Handler => new(_repository);

    [Fact]
    public async Task Run_is_returned_as_plain_data()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var details = await Handler.HandleAsync(new GetReviewRun(runId), CancellationToken.None);

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
    public async Task Unknown_run_returns_null()
    {
        var details = await Handler.HandleAsync(new GetReviewRun(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(details);
    }
}
