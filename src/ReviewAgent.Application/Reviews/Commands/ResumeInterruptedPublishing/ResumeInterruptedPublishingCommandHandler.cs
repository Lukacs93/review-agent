using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Application.Reviews.Commands.ResumeInterruptedPublishing;

/// <summary>Schedules every run being published for the publishing worker; returns how many it scheduled.</summary>
public sealed class ResumeInterruptedPublishingCommandHandler(
    IReviewRunRepository repository,
    IPublishingScheduler scheduler) : ICommandHandler<ResumeInterruptedPublishingCommand, int>
{
    public async Task<int> HandleAsync(
        ResumeInterruptedPublishingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var publishingIds = await repository.FindPublishingIdsAsync(cancellationToken);
        foreach (var runId in publishingIds)
        {
            await scheduler.ScheduleAsync(runId.Value, cancellationToken);
        }

        return publishingIds.Count;
    }
}
