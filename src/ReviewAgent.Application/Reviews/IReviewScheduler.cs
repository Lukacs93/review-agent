namespace ReviewAgent.Application.Reviews;

/// <summary>
/// Hands started review runs to the agent. Starting a review only schedules the run, so the request returns at
/// once and the agent reviews in the background.
/// </summary>
public interface IReviewScheduler
{
    ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken);
}
