namespace ReviewAgent.Infrastructure.Persistence.RunLog;

/// <summary>One log line written while a review run was running.</summary>
// Kept apart from the event store: a log line is technical detail, not part of the run's state.
internal sealed class StoredRunLogEntry
{
    /// <summary>Increases with each line, so the lines read back in the order they were saved.</summary>
    public long Id { get; init; }

    public required Guid RunId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The log level's name, for example <c>Warning</c>.</summary>
    public required string Level { get; init; }

    /// <summary>The logger's category: the class that wrote the line.</summary>
    public required string Category { get; init; }

    public required string Message { get; init; }

    /// <summary>The exception logged with the line, with its stack trace; <see langword="null"/> if none.</summary>
    public string? Exception { get; init; }
}
