using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Integration.Tests;

/// <summary>
/// Runs are not queued for publishing in the API tests, so nothing is written and the runs stay as the tests left
/// them.
/// </summary>
internal sealed class NoPublishingScheduler : IPublishingScheduler
{
    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
