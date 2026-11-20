using ReviewAgent.Application.Reviews.Queries.GetRunLog;

namespace ReviewAgent.Application.Tests.Fakes;

/// <summary>Log lines kept in memory per run, in the order they were added.</summary>
internal sealed class InMemoryRunLogReader : IRunLogReader
{
    private readonly Dictionary<Guid, List<RunLogEntryDetails>> _linesByRun = [];

    public void Add(Guid runId, RunLogEntryDetails line)
    {
        if (!_linesByRun.TryGetValue(runId, out var lines))
        {
            lines = [];
            _linesByRun[runId] = lines;
        }

        lines.Add(line);
    }

    public Task<IReadOnlyList<RunLogEntryDetails>> ReadAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RunLogEntryDetails>>(
            _linesByRun.TryGetValue(runId, out var lines) ? lines : []);
}
