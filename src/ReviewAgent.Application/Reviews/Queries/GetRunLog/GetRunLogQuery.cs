namespace ReviewAgent.Application.Reviews.Queries.GetRunLog;

/// <summary>The log lines written while a review run was running.</summary>
public sealed record GetRunLogQuery(Guid RunId);
