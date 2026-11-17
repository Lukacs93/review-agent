using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Scheduling;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.StartReview;

/// <summary>Creates and saves a new review run, then schedules it for the agent; returns its ID.</summary>
public sealed class StartReviewCommandHandler(
    IReviewRunRepository repository,
    IReviewScheduler scheduler,
    TimeProvider timeProvider) : ICommandHandler<StartReviewCommand, Guid>
{
    public async Task<Guid> HandleAsync(StartReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pullRequest = new PullRequestRef(
            command.RepositoryOwner,
            command.RepositoryName,
            command.PullRequestNumber);
        var run = ReviewRun.Start(ReviewRunId.New(), pullRequest, timeProvider.GetUtcNow());

        await repository.SaveAsync(run, cancellationToken);
        await scheduler.ScheduleAsync(run.Id.Value, cancellationToken);
        return run.Id.Value;
    }
}
