using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Queries.GetRunLog;

/// <summary>
/// Returns a review run's log lines, or <see langword="null"/> if the run does not exist, so an unknown run is told
/// apart from a run without log lines.
/// </summary>
public sealed class GetRunLogQueryHandler(IReviewRunRepository repository, IRunLogReader reader)
    : IQueryHandler<GetRunLogQuery, IReadOnlyList<RunLogEntryDetails>?>
{
    public async Task<IReadOnlyList<RunLogEntryDetails>?> HandleAsync(
        GetRunLogQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = await repository.FindAsync(new ReviewRunId(query.RunId), cancellationToken);
        return run is null ? null : await reader.ReadAsync(query.RunId, cancellationToken);
    }
}
