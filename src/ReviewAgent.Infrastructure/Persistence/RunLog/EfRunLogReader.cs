using Microsoft.EntityFrameworkCore;
using ReviewAgent.Application.Reviews.Queries.GetRunLog;

namespace ReviewAgent.Infrastructure.Persistence.RunLog;

internal sealed class EfRunLogReader(ReviewAgentDbContext dbContext) : IRunLogReader
{
    public async Task<IReadOnlyList<RunLogEntryDetails>> ReadAsync(Guid runId, CancellationToken cancellationToken) =>
        await dbContext.RunLogEntries
            .AsNoTracking()
            .Where(entry => entry.RunId == runId)
            .OrderBy(entry => entry.Id)
            .Select(entry => new RunLogEntryDetails(
                entry.Timestamp,
                entry.Level,
                entry.Category,
                entry.Message,
                entry.Exception))
            .ToListAsync(cancellationToken);
}
