using ReviewAgent.Application.Reviews.Commands.FailInterruptedReviews;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FailInterruptedReviews;

public sealed class FailInterruptedReviewsCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();

    private FailInterruptedReviewsCommandHandler Handler =>
        new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Running_reviews_are_failed_as_interrupted()
    {
        var firstRunId = await TestReviewRuns.SaveRunningRunAsync(_repository);
        var (secondRunId, _) = await TestReviewRuns.SaveRunningRunWithOneCommentAsync(_repository);

        var failed = await Handler.HandleAsync(new FailInterruptedReviewsCommand(), CancellationToken.None);

        Assert.Equal(2, failed);
        foreach (var runId in new[] { firstRunId, secondRunId })
        {
            var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
            Assert.NotNull(run);
            Assert.Equal(ReviewStatus.Failed, run.Status);
            Assert.Equal(InterruptedReviewErrors.Interrupted, run.FailureReason);
        }
    }

    [Fact]
    public async Task Finished_reviews_are_left_alone()
    {
        var (runId, _) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var failed = await Handler.HandleAsync(new FailInterruptedReviewsCommand(), CancellationToken.None);

        Assert.Equal(0, failed);
        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
    }
}
