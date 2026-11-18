using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews;

/// <summary>Loads and saves review runs. A run is stored as the sequence of its events.</summary>
public interface IReviewRunRepository
{
    /// <summary>Rebuilds the run from its stored events, or returns <see langword="null"/> if it does not exist.</summary>
    Task<ReviewRun?> FindAsync(ReviewRunId id, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the run's uncommitted events. Throws <see cref="ConcurrencyConflictException"/> when another save
    /// has appended events to the same run since it was loaded.
    /// </summary>
    Task SaveAsync(ReviewRun run, CancellationToken cancellationToken);

    /// <summary>The IDs of the runs the agent has not finished or failed yet.</summary>
    Task<IReadOnlyList<ReviewRunId>> FindRunningIdsAsync(CancellationToken cancellationToken);
}
