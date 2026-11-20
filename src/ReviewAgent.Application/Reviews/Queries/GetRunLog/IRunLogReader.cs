namespace ReviewAgent.Application.Reviews.Queries.GetRunLog;

/// <summary>Reads the log lines saved for a review run.</summary>
public interface IRunLogReader
{
    /// <summary>The run's log lines in the order they were written; empty when there are none.</summary>
    Task<IReadOnlyList<RunLogEntryDetails>> ReadAsync(Guid runId, CancellationToken cancellationToken);
}
