using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands;

public sealed class DiscardCommentHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private DiscardCommentHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Discarded_comment_is_saved()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new DiscardComment(runId, commentId), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(CommentDecision.Discarded, Assert.Single(run.Comments).Decision);
    }
}
