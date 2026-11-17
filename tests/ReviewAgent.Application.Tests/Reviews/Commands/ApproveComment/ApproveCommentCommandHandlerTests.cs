using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Commands.ApproveComment;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Application.Tests.Reviews.Commands.ApproveComment;

public sealed class ApproveCommentCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private ApproveCommentCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Approved_comment_is_saved()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new ApproveCommentCommand(runId, commentId), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(CommentDecision.Approved, Assert.Single(run.Comments).Decision);
    }

    [Fact]
    public async Task Unknown_run_is_rejected()
    {
        var runId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<ReviewRunNotFoundException>(
            () => Handler.HandleAsync(new ApproveCommentCommand(runId, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(ReviewRunRepositoryErrors.RunNotFound(new ReviewRunId(runId)), error.Message);
    }

    [Fact]
    public async Task Domain_rules_still_apply()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new ApproveCommentCommand(runId, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(ReviewRunErrors.DecideWhenNotFinished, error.Message);
    }
}
