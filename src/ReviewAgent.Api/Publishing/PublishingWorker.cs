using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.PublishReview;
using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Api.Publishing;

/// <summary>
/// Publishes the queued runs one at a time, each in its own DI scope. One at a time, so the same run is never
/// published twice at once.
/// </summary>
internal sealed partial class PublishingWorker(
    IScheduledPublishingReader scheduledRuns,
    IServiceScopeFactory scopeFactory,
    ILogger<PublishingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in scheduledRuns.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            try
            {
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PublishReviewCommand>>();
                await handler.HandleAsync(new PublishReviewCommand(runId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The handler has recorded the failure on the run when it could; the developer sees the reason and
                // can ask again. Logged here, and the worker goes on with the next run.
                LogPublishingFailed(logger, runId, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing run {RunId} failed.")]
    private static partial void LogPublishingFailed(ILogger logger, Guid runId, Exception exception);
}
