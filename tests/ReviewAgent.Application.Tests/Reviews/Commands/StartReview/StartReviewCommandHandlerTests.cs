using ReviewAgent.Application.Reviews.Commands.StartReview;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Application.Tests.Reviews.Commands.StartReview;

public sealed class StartReviewCommandHandlerTests
{
    private readonly InMemoryReviewRunRepository _repository = new();
    private readonly RecordingReviewScheduler _scheduler = new();

    private StartReviewCommandHandler Handler =>
        new(_repository, _scheduler, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Started_run_is_saved_and_running()
    {
        var command = new StartReviewCommand(
            TestReviewRuns.RepositoryOwner,
            TestReviewRuns.RepositoryName,
            TestReviewRuns.PullRequestNumber);

        var runId = await Handler.HandleAsync(command, CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(ReviewStatus.Running, run.Status);
        Assert.Equal(
            new PullRequestRef(
                TestReviewRuns.RepositoryOwner,
                TestReviewRuns.RepositoryName,
                TestReviewRuns.PullRequestNumber),
            run.PullRequest);
        Assert.Equal(TestReviewRuns.Now, run.StartedAt);
    }

    [Fact]
    public async Task Started_run_is_queued_for_the_agent()
    {
        var command = new StartReviewCommand(
            TestReviewRuns.RepositoryOwner,
            TestReviewRuns.RepositoryName,
            TestReviewRuns.PullRequestNumber);

        var runId = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(runId, Assert.Single(_scheduler.RunIds));
    }

    [Fact]
    public async Task Invalid_pull_request_is_rejected()
    {
        var command = new StartReviewCommand(" ", TestReviewRuns.RepositoryName, TestReviewRuns.PullRequestNumber);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(ValueObjectErrors.RepositoryOwnerRequired, error.Message);
        Assert.Empty(_scheduler.RunIds);
    }
}
