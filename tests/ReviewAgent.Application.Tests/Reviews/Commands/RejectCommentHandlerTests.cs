using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands;

public sealed class RejectCommentHandlerTests
{
    private const string Reason = "The query does not change state, so CQRS-01 is not broken.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private RejectCommentHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Rejected_comment_is_saved_with_the_reason()
    {
        var (runId, commentId) = await TestReviewRuns.SaveRunningRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new RejectComment(runId, commentId, Reason), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(Reason, Assert.Single(run.Comments).RejectionReason);
    }
}
