namespace ReviewAgent.Infrastructure.Persistence.Reviews;

/// <summary>
/// One row per review run. Its version is the number of stored events. Saving checks it as a concurrency token,
/// so a save that started from an outdated version fails instead of appending conflicting events.
/// </summary>
public sealed class StoredReviewRun
{
    public required Guid Id { get; init; }

    public required int Version { get; set; }
}
