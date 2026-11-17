using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Integration.Tests;

/// <summary>
/// Started reviews are not queued in the API tests, so no model is called and the runs stay as the tests left them.
/// </summary>
internal sealed class NoReviewScheduler : IReviewScheduler
{
    public ValueTask ScheduleAsync(Guid runId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
