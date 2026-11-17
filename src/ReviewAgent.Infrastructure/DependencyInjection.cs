using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Agent;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
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

    /// <summary>
    /// Everything the Infrastructure layer provides: the database, the pull request sources and the review agent.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services.AddPersistence().AddSourceControl().AddReviewAgent();

    /// <summary>The event store: the EF Core context and the review run repository.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The connection string is read when a context is first created, not here, so a value configured after
        // this registration still applies.
        services.AddDbContext<ReviewAgentDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(DatabaseErrors.ConnectionStringMissing(ConnectionStringName));

            options.UseSqlite(connectionString);
        });

        services.AddScoped<IReviewRunRepository, EfReviewRunRepository>();

        return services;
    }

    /// <summary>
    /// The pull request sources: saved (offline) and GitHub. The configuration decides which one
    /// <see cref="IPullRequestSource"/> resolves to.
    /// </summary>
    public static IServiceCollection AddSourceControl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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

    /// <summary>
    /// The review agent: the model client from the <c>Agent</c> section of the configuration, and the connection to
    /// the MCP server's tools.
    /// </summary>
    public static IServiceCollection AddReviewAgent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AgentOptions>()
            .BindConfiguration(AgentOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // No retry handler here: an MCP call that half succeeded must not be repeated behind the model's back.
        services.AddHttpClient(McpPullRequestToolsConnector.HttpClientName);

        // The model's requests: the same standard resilience handler as GitHub's (retries with backoff on 429, 503
        // and timeouts, honouring Retry-After, and a circuit breaker), with timeouts long enough for a model.
        // HttpClient's own 100-second timeout would cut through the retries, so the handler's timeouts replace it.
        services.AddHttpClient(
                OpenAICompatibleChatClientFactory.HttpClientName,
                client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var agent = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;

                resilience.AttemptTimeout.Timeout = agent.RequestTimeout;
                resilience.TotalRequestTimeout.Timeout = agent.Timeout;

                // The handler requires the breaker to sample at least two attempts' worth of time.
                resilience.CircuitBreaker.SamplingDuration = agent.RequestTimeout * 2;
            });

        services.AddSingleton<IAgentChatClientFactory, OpenAICompatibleChatClientFactory>();
        services.AddSingleton<IPullRequestToolsConnector, McpPullRequestToolsConnector>();
        services.AddScoped<ReviewAgentCommands>();
        services.AddScoped<IReviewAgent, ReviewAgentRunner>();

        return services;
    }
}
