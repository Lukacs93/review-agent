using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Scheduling;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.RequestPublishing;

/// <summary>
/// Records the request, then schedules the run for the publishing worker. Recorded first, so the request is not
/// lost if the application stops before the run is published: it is scheduled again at the next start.
/// </summary>
public sealed class RequestPublishingCommandHandler(
    IReviewRunRepository repository,
    IPublishingScheduler scheduler,
    TimeProvider timeProvider) : ICommandHandler<RequestPublishingCommand>
{
    public async Task HandleAsync(RequestPublishingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        run.RequestPublishing(timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);

        await scheduler.ScheduleAsync(run.Id.Value, cancellationToken);
    }
}
