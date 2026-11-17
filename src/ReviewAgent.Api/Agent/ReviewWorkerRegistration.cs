using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Api.Agent;

internal static class ReviewWorkerRegistration
{
    /// <summary>The queue of started reviews and the background worker that runs the agent on them.</summary>
    public static IServiceCollection AddReviewWorker(this IServiceCollection services)
    {
        services.AddSingleton<ChannelReviewScheduler>();

        // One in-memory channel behind both ends: the API writes to it, the worker reads from it.
        services.AddSingleton<IReviewScheduler>(
            serviceProvider => serviceProvider.GetRequiredService<ChannelReviewScheduler>());
        services.AddSingleton<IScheduledReviewReader>(
            serviceProvider => serviceProvider.GetRequiredService<ChannelReviewScheduler>());

        services.AddHostedService<ReviewWorker>();

        return services;
    }
}
