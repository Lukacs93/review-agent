using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Persistence.Errors;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.SourceControl;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.Saved;

namespace ReviewAgent.Infrastructure;

/// <summary>
/// Registers the services of the Infrastructure layer. This class and <see cref="DatabaseMigrations"/> are the
/// layer's only public entry points; everything else is internal.
/// </summary>
public static class DependencyInjection
{
    /// <summary>The name of the connection string in the configuration: <c>ConnectionStrings:ReviewAgent</c>.</summary>
    public const string ConnectionStringName = "ReviewAgent";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The connection string is read when a context is first created, not here, so tests can override it.
        services.AddDbContext<ReviewAgentDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(DatabaseErrors.ConnectionStringMissing(ConnectionStringName));

            options.UseSqlite(connectionString);
        });

        services.AddScoped<IReviewRunRepository, EfReviewRunRepository>();

        services.AddOptions<PullRequestSourceOptions>()
            .BindConfiguration(PullRequestSourceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;
            return new SavedPullRequestSource(Path.GetFullPath(options.SavedDirectory, AppContext.BaseDirectory));
        });

        // The standard resilience handler retries transient failures (5xx, 408, 429) with backoff, honours
        // Retry-After, and adds timeouts and a circuit breaker.
        services.AddHttpClient<GitHubPullRequestSource>((serviceProvider, client) =>
                GitHubPullRequestSource.ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.GitHub))
            .AddStandardResilienceHandler();

        // The rest of the application only sees IPullRequestSource; the configuration decides which one it gets.
        services.AddTransient<IPullRequestSource>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.Source switch
            {
                PullRequestSourceKind.GitHub => serviceProvider.GetRequiredService<GitHubPullRequestSource>(),
                _ => serviceProvider.GetRequiredService<SavedPullRequestSource>(),
            });

        return services;
    }
}
