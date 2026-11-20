using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Persistence.RunLog;

namespace ReviewAgent.Infrastructure.Logging;

/// <summary>
/// Saves the review runs' log lines from <see cref="RunLogQueue"/> to the database in the background, all waiting
/// lines in one go.
/// </summary>
// Its own log lines are written outside any review run, so they do not come back to the queue.
internal sealed partial class RunLogWriter(
    RunLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<RunLogWriter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            await SaveWaitingLinesAsync(stoppingToken);
        }
    }

    /// <summary>Saves the lines waiting in the queue. A failed save is logged, and the lines are lost.</summary>
    // Losing a few log lines is better than stopping the reviews or retrying without end while the database is down.
    internal async Task SaveWaitingLinesAsync(CancellationToken cancellationToken)
    {
        var lines = new List<StoredRunLogEntry>();
        while (queue.Reader.TryRead(out var line))
        {
            lines.Add(line);
        }

        var dropped = queue.TakeDroppedCount();
        if (dropped > 0)
        {
            LogLinesDropped(logger, dropped);
        }

        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReviewAgentDbContext>();
            dbContext.RunLogEntries.AddRange(lines);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSaveFailed(logger, lines.Count, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} review run log lines were dropped: the queue was full.")]
    private static partial void LogLinesDropped(ILogger logger, long count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} review run log lines could not be saved.")]
    private static partial void LogSaveFailed(ILogger logger, int count, Exception exception);
}
