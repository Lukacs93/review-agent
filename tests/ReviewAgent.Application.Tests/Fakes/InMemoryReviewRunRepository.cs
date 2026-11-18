using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Events;

namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>
/// Keeps review events in memory, with the same contract as the real repository: runs are rebuilt from their
/// events, and a save based on an outdated version is rejected.
/// </summary>
internal sealed class InMemoryReviewRunRepository : IReviewRunRepository
{
    private readonly Dictionary<ReviewRunId, List<ReviewEvent>> _eventsByRun = [];

    public Task<ReviewRun?> FindAsync(ReviewRunId id, CancellationToken cancellationToken) =>
        Task.FromResult(_eventsByRun.TryGetValue(id, out var events) ? ReviewRun.FromHistory(events) : null);

    public Task SaveAsync(ReviewRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var newEvents = run.UncommittedEvents;
        var loadedVersion = run.Version - newEvents.Count;
        List<ReviewEvent> events = _eventsByRun.TryGetValue(run.Id, out var stored) ? stored : [];

        if (events.Count != loadedVersion)
        {
            throw new ConcurrencyConflictException(ReviewRunRepositoryErrors.ConcurrentChange(run.Id));
        }

        events.AddRange(newEvents);
        _eventsByRun[run.Id] = events;
        run.ClearUncommittedEvents();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReviewRunId>> FindRunningIdsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ReviewRunId> runningIds = _eventsByRun
            .Where(pair => !pair.Value.Any(reviewEvent => reviewEvent is ReviewFinished or ReviewFailed))
            .Select(pair => pair.Key)
            .ToList();

        return Task.FromResult(runningIds);
    }

    public Task<IReadOnlyList<ReviewRunId>> FindPublishingIdsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ReviewRunId> publishingIds = _eventsByRun
            .Where(pair => ReviewRun.FromHistory(pair.Value).Status == ReviewStatus.Publishing)
            .Select(pair => pair.Key)
            .ToList();

        return Task.FromResult(publishingIds);
    }
}
