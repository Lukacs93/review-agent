namespace ReviewAgent.Application.Reviews.Queries.GetRunLog;

/// <summary>One log line of a review run, as plain data for the UI.</summary>
public sealed record RunLogEntryDetails(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception);
