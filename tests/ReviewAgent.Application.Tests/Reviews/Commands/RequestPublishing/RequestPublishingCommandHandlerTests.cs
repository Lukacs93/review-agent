using ReviewAgent.Application.Reviews.Commands.RequestPublishing;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Application.Tests.Reviews.Commands.RequestPublishing;

public sealed class RequestPublishingCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();
    private readonly RecordingPublishingScheduler _scheduler = new();

    private RequestPublishingCommandHandler Handler =>
        new(_repository, _scheduler, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Requested_publishing_is_saved_and_scheduled()
    {
        var runId = await TestReviewRuns.SaveApprovedRunAsync(_repository);

        await Handler.HandleAsync(new RequestPublishingCommand(runId), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.Publishing, run.Status);
        Assert.Equal(runId, Assert.Single(_scheduler.RunIds));
    }

    [Fact]
    public async Task Review_with_undecided_comments_is_not_scheduled()
    {
        var (runId, _) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new RequestPublishingCommand(runId), CancellationToken.None));

        Assert.Equal(ReviewRunErrors.UndecidedComments, error.Message);
        Assert.Empty(_scheduler.RunIds);
    }
}
