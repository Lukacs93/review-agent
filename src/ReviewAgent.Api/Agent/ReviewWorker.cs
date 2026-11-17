using ReviewAgent.Application.Reviews;

namespace ReviewAgent.Api.Agent;

/// <summary>
/// Runs the agent on the queued review runs, one at a time, each in its own DI scope (its own database context).
/// </summary>
internal sealed partial class ReviewWorker(
    IScheduledReviewReader scheduledReviews,
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in scheduledReviews.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            try
            {
                var agent = scope.ServiceProvider.GetRequiredService<IReviewAgent>();
                await agent.ReviewAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The agent handles its own errors by failing the run. This catch is the last line of defence: if
                // even that fails, for example because the database is down, log it and go on with the next run.
                // Without it, one exception would stop the worker, and no review would run until a restart.
                LogReviewCrashed(logger, runId, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The review of run {RunId} stopped with an unexpected error.")]
    private static partial void LogReviewCrashed(ILogger logger, Guid runId, Exception exception);
}
