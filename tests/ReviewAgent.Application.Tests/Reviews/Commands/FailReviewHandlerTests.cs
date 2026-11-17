using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands;

public sealed class FailReviewHandlerTests
{
    private const string Reason = "The model could not be reached.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private FailReviewHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Failed_review_keeps_the_reason()
    {
        var runId = await TestReviewRuns.SaveRunningRunAsync(_repository);

        await Handler.HandleAsync(new FailReview(runId, Reason), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal(Reason, run.FailureReason);
    }
}
