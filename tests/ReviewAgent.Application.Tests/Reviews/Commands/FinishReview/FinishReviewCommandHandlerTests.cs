using ReviewAgent.Application.Reviews.Commands.FinishReview;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FinishReview;

public sealed class FinishReviewCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private FinishReviewCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Finished_review_awaits_approval()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        await Handler.HandleAsync(new FinishReviewCommand(runId, TestReviewRuns.Summary), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(TestReviewRuns.Summary, run.Summary);
    }
}
