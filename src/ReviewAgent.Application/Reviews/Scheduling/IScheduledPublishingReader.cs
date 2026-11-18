namespace ReviewAgent.Application.Reviews.Scheduling;

/// <summary>
/// Hands the runs scheduled with <see cref="IPublishingScheduler"/> to the publishing worker, in order.
/// </summary>
public interface IScheduledPublishingReader
{
    /// <summary>The scheduled run IDs, one by one as they are scheduled, until cancelled.</summary>
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
