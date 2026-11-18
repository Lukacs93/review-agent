using Microsoft.EntityFrameworkCore;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Events;

namespace ReviewAgent.Infrastructure.Persistence.Reviews;

/// <summary>
/// Stores review runs as events through EF Core. Concurrent changes are detected with optimistic concurrency:
/// the run's stored version must still be the one it was loaded with.
/// </summary>
internal sealed class EfReviewRunRepository(ReviewAgentDbContext dbContext) : IReviewRunRepository
{
    // A run stops running when the agent finishes or fails it; publishing only comes after finishing.
    private static readonly string[] _runEndingEventTypes =
    [
        ReviewEventSerializer.TypeNameOf(typeof(ReviewFinished)),
        ReviewEventSerializer.TypeNameOf(typeof(ReviewFailed)),
    ];

    public async Task<ReviewRun?> FindAsync(ReviewRunId id, CancellationToken cancellationToken)
    {
        var storedEvents = await dbContext.ReviewEvents
            .AsNoTracking()
            .Where(storedEvent => storedEvent.RunId == id.Value)
            .OrderBy(storedEvent => storedEvent.Version)
            .ToListAsync(cancellationToken);

        if (storedEvents.Count == 0)
        {
            return null;
        }

        return ReviewRun.FromHistory(storedEvents.Select(
            storedEvent => ReviewEventSerializer.Deserialize(storedEvent.EventType, storedEvent.Data)));
    }

    public async Task SaveAsync(ReviewRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var newEvents = run.UncommittedEvents;
        if (newEvents.Count == 0)
        {
            return;
        }

        // The run's version already includes the new events; before them it was at the version it was loaded with.
        var loadedVersion = run.Version - newEvents.Count;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // The version is advanced first, so a conflicting save fails before any of its events is written.
            await AdvanceVersionAsync(run.Id, loadedVersion, run.Version, cancellationToken);

            dbContext.ReviewEvents.AddRange(ToRows(run.Id, loadedVersion, newEvents));
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(ReviewRunRepositoryErrors.ConcurrentChange(run.Id), exception);
        }
        finally
        {
            // Runs are always rebuilt from their events, so nothing needs to stay tracked after a save.
            dbContext.ChangeTracker.Clear();
        }

        run.ClearUncommittedEvents();
    }

    public async Task<IReadOnlyList<ReviewRunId>> FindRunningIdsAsync(CancellationToken cancellationToken)
    {
        // Runs without an event that ended the running phase; read from the events, so no extra column is needed.
        var ids = await dbContext.ReviewRuns
            .AsNoTracking()
            .Where(storedRun => !dbContext.ReviewEvents.Any(storedEvent =>
                storedEvent.RunId == storedRun.Id && _runEndingEventTypes.Contains(storedEvent.EventType)))
            .Select(storedRun => storedRun.Id)
            .ToListAsync(cancellationToken);

        return ids.Select(id => new ReviewRunId(id)).ToList();
    }

    private async Task AdvanceVersionAsync(
        ReviewRunId id,
        int loadedVersion,
        int newVersion,
        CancellationToken cancellationToken)
    {
        if (loadedVersion == 0)
        {
            dbContext.ReviewRuns.Add(new StoredReviewRun { Id = id.Value, Version = newVersion });
        }
        else
        {
            // Attached with the loaded version, so EF Core sends:
            // UPDATE ReviewRuns SET Version = @newVersion WHERE Id = @id AND Version = @loadedVersion
            // If another save advanced the version meanwhile, no row matches and DbUpdateConcurrencyException is thrown.
            var storedRun = new StoredReviewRun { Id = id.Value, Version = loadedVersion };
            dbContext.ReviewRuns.Attach(storedRun);
            storedRun.Version = newVersion;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<StoredReviewEvent> ToRows(
        ReviewRunId id,
        int loadedVersion,
        IEnumerable<ReviewEvent> newEvents) =>
        newEvents.Select((reviewEvent, index) => new StoredReviewEvent
        {
            RunId = id.Value,
            Version = loadedVersion + index + 1,
            EventType = ReviewEventSerializer.TypeNameOf(reviewEvent),
            Data = ReviewEventSerializer.Serialize(reviewEvent),
            OccurredAt = reviewEvent.OccurredAt,
        });
}
