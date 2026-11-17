
namespace ReviewAgent.Application.Reviews.Scheduling;

/// <summary>
/// Hands the review runs scheduled with <see cref="IReviewScheduler"/> to the worker, in order.
/// </summary>
public interface IScheduledReviewReader
{
    /// <summary>The scheduled run IDs, one by one as they are scheduled, until cancelled.</summary>
    /// <remarks>
    /// An <see cref="IAsyncEnumerable{T}"/>, because the runs arrive over time and are not known in advance:
    /// the worker waits for the next one without holding a thread.
    /// </remarks>
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
