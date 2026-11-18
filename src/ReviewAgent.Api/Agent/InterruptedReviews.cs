using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.FailInterruptedReviews;

namespace ReviewAgent.Api.Agent;

internal static partial class InterruptedReviews
{
    /// <summary>
    /// Fails the reviews the previous run of the application left running. Called at startup, before requests are
    /// served, so a review started now cannot be taken for an interrupted one. Correct for a single instance: with
    /// several instances, it would also fail the reviews another instance is running.
    /// </summary>
    public static async Task FailInterruptedReviewsAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<FailInterruptedReviewsCommand, int>>();

        var failed = await handler.HandleAsync(new FailInterruptedReviewsCommand(), cancellationToken);
        if (failed > 0)
        {
            LogInterruptedReviewsFailed(logger, failed);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed {Count} reviews that were interrupted by a restart.")]
    private static partial void LogInterruptedReviewsFailed(ILogger logger, int count);
}
