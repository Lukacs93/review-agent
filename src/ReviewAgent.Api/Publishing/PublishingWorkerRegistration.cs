using ReviewAgent.Application.Reviews.Scheduling;

namespace ReviewAgent.Api.Publishing;

internal static class PublishingWorkerRegistration
{
    /// <summary>The queue of runs to publish and the background worker that publishes them.</summary>
    public static IServiceCollection AddPublishingWorker(this IServiceCollection services)
    {
        services.AddSingleton<ChannelPublishingScheduler>();

        // One in-memory channel behind both ends: the API writes to it, the worker reads from it.
        services.AddSingleton<IPublishingScheduler>(
            serviceProvider => serviceProvider.GetRequiredService<ChannelPublishingScheduler>());
        services.AddSingleton<IScheduledPublishingReader>(
            serviceProvider => serviceProvider.GetRequiredService<ChannelPublishingScheduler>());

        services.AddHostedService<PublishingWorker>();

        return services;
    }
}
