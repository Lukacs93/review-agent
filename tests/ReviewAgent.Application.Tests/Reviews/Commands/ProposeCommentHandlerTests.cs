using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Tests.Reviews.Commands;

public sealed class ProposeCommentHandlerTests
{
    private const string SuggestedFix = "Call an application handler instead.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private ProposeCommentHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Proposed_comment_is_saved()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        var commentId = await Handler.HandleAsync(Command(runId, TestReviewRuns.Line), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        var comment = Assert.Single(run.Comments);
        Assert.Equal(commentId, comment.Id.Value);
        Assert.Equal(new CodeLocation(TestReviewRuns.FilePath, TestReviewRuns.Line), comment.Location);
        Assert.Equal(new RuleId(TestReviewRuns.Rule), comment.Rule);
        Assert.Equal(Severity.Error, comment.Severity);
        Assert.Equal(TestReviewRuns.CommentBody, comment.Body);
        Assert.Equal(SuggestedFix, comment.SuggestedFix);
    }

    [Fact]
    public async Task Invalid_location_is_rejected()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(Command(runId, line: 0), CancellationToken.None));

        Assert.Equal(ValueObjectErrors.LineNotPositive, error.Message);
    }

    [Fact]
    public async Task Comment_cannot_be_proposed_after_the_review_finished()
    {
        var (runId, _) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(Command(runId, TestReviewRuns.Line), CancellationToken.None));

        Assert.Equal(ReviewRunErrors.ProposeWhenNotRunning, error.Message);
    }

    private static ProposeComment Command(Guid runId, int line) =>
        new(
            runId,
            TestReviewRuns.FilePath,
            line,
            TestReviewRuns.Rule,
            Severity.Error,
            TestReviewRuns.CommentBody,
            SuggestedFix);
}
