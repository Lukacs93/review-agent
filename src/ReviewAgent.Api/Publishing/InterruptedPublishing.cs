using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.ResumeInterruptedPublishing;

namespace ReviewAgent.Api.Publishing;

internal static partial class InterruptedPublishing
{
    /// <summary>
    /// Queues again the runs the previous run of the application was publishing. Called at startup; the worker
    /// checks the pull request first, so a review that already went out is only recorded, not published again.
    /// </summary>
    public static async Task ResumeInterruptedPublishingAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ResumeInterruptedPublishingCommand, int>>();

        var resumed = await handler.HandleAsync(new ResumeInterruptedPublishingCommand(), cancellationToken);
        if (resumed > 0)
        {
            LogInterruptedPublishingResumed(logger, resumed);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resumed publishing {Count} reviews after a restart.")]
    private static partial void LogInterruptedPublishingResumed(ILogger logger, int count);
}
