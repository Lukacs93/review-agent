using ReviewAgent.Application.Reviews.Commands.ResumeInterruptedPublishing;
using ReviewAgent.Application.Tests.Fakes;

namespace ReviewAgent.Application.Tests.Reviews.Commands.ResumeInterruptedPublishing;

public sealed class ResumeInterruptedPublishingCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();
    private readonly RecordingPublishingScheduler _scheduler = new();

    private ResumeInterruptedPublishingCommandHandler Handler => new(_repository, _scheduler);

    [Fact]
    public async Task Only_runs_being_published_are_scheduled_again()
    {
        var publishingRunId = await TestReviewRuns.SavePublishingRunAsync(_repository);
        await TestReviewRuns.SaveApprovedRunAsync(_repository);
        await TestReviewRuns.SaveRunningRunAsync(_repository);

        var resumed = await Handler.HandleAsync(new ResumeInterruptedPublishingCommand(), CancellationToken.None);

        Assert.Equal(1, resumed);
        Assert.Equal(publishingRunId, Assert.Single(_scheduler.RunIds));
    }
}
