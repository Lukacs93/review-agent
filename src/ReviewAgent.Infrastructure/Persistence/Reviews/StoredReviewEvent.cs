namespace ReviewAgent.Infrastructure.Persistence.Reviews;

/// <summary>One row of the event store: a review event serialized as JSON. Rows are only ever inserted.</summary>
public sealed class StoredReviewEvent
{
    public required Guid RunId { get; init; }

    /// <summary>The position of the event within its run, starting at 1. Together with the run ID it is the key.</summary>
    public required int Version { get; init; }

    /// <summary>The stable name of the event type; see <see cref="ReviewEventSerializer"/>.</summary>
    public required string EventType { get; init; }

    public required string Data { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
