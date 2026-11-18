namespace ReviewAgent.Application.Reviews.Scheduling;

/// <summary>
/// Hands the runs to publish to the publishing worker. The request that asks for publishing only schedules the run,
/// so it returns at once, and the review is published in the background.
/// </summary>
public interface IPublishingScheduler
{
    ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken);
}
