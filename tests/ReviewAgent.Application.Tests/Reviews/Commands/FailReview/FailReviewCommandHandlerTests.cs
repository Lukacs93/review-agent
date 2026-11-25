using ReviewAgent.Application.Reviews.Commands.FailReview;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.FailReview;

public sealed class FailReviewCommandHandlerTests
{
    private const string Reason = "The model could not be reached.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private FailReviewCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Failed_review_keeps_the_reason()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        await Handler.HandleAsync(
            new FailReviewCommand(runId, Reason, ReviewFailureKind.ModelService),
            CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal(Reason, run.FailureReason);
    }
}
