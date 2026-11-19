using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Agent;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Persistence.Errors;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.SourceControl;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;
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
    /// Everything the API needs from the Infrastructure layer: the database, the pull request sources, the review
    /// publishers and the review agent.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services.AddPersistence().AddSourceControl().AddReviewPublishing().AddReviewAgent();

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
    /// The pull request sources, saved to disk and GitHub, and where the team keeps its rules. The configuration
    /// decides which source <see cref="IPullRequestSource"/> resolves to. Read-only: the MCP server uses only this.
    /// </summary>
    public static IServiceCollection AddSourceControl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The "PullRequests" section of the configuration, bound to PullRequestSourceOptions and checked against its
        // attributes when the application starts, so a missing setting stops it right away, not at the first request.
        services.AddOptions<PullRequestSourceOptions>()
            .BindConfiguration(PullRequestSourceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The "PullRequests:TeamRulesPath" setting, bound and checked the same way.
        services.AddOptions<TeamRulesOptions>()
            .BindConfiguration(TeamRulesOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // A singleton: it keeps no state but the directory, and reads the files on every call, so one instance can
        // serve the whole application, from any thread.
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;
            return new SavedPullRequestSource(Path.GetFullPath(options.SavedDirectory, AppContext.BaseDirectory));
        });

        // The standard resilience handler retries transient failures (5xx, 408, 429) with backoff, honours
        // Retry-After, and adds timeouts and a circuit breaker. Reading needs only the read token.
        services.AddHttpClient<GitHubPullRequestSource>((serviceProvider, client) =>
            {
                var github = serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.GitHub;
                GitHubPullRequestSource.ConfigureClient(client, github.ApiUrl, github.ReadToken);
            })
            .AddStandardResilienceHandler();

        // The rest of the application only sees IPullRequestSource; the configuration decides which one it gets.
        // Transient, because the GitHub classes are typed HTTP clients, which AddHttpClient registers as transient:
        // each gets a client from the factory, which renews the connection handlers from time to time. A singleton
        // here would keep the first GitHub instance, and its old handler, for the application's whole life.
        services.AddTransient<IPullRequestSource>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.Source switch
            {
                PullRequestSourceKind.GitHub => serviceProvider.GetRequiredService<GitHubPullRequestSource>(),
                _ => serviceProvider.GetRequiredService<SavedPullRequestSource>(),
            });

        return services;
    }

    /// <summary>
    /// The review publishers: to GitHub, or to disk for saved pull requests. The same
    /// <c>PullRequests:Source</c> setting as the sources' decides which one <see cref="IReviewPublisher"/>
    /// resolves to, so call it after <see cref="AddSourceControl"/>. Only the API publishes.
    /// </summary>
    public static IServiceCollection AddReviewPublishing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Publishing to GitHub needs the write token, so its absence stops the application at startup instead of
        // failing the first publishing. Checked here, not with the other settings, because the MCP server reads the
        // same settings and never gets the write token.
        services.AddOptions<PullRequestSourceOptions>()
            .Validate(
                options => options.Source != PullRequestSourceKind.GitHub
                    || !string.IsNullOrWhiteSpace(options.GitHub.WriteToken),
                GitHubErrors.WriteTokenRequired)
            .ValidateOnStart();

        // A singleton for the same reason as the saved source.
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;
            return new SavedReviewPublisher(Path.GetFullPath(options.SavedDirectory, AppContext.BaseDirectory));
        });

        // The source's client, but with the write token, and a request that changes something is not retried: a
        // review that timed out may still have been created. The next publishing checks the pull request for it
        // first.
        services.AddHttpClient<GitHubReviewPublisher>((serviceProvider, client) =>
            {
                var github = serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.GitHub;
                GitHubPullRequestSource.ConfigureClient(client, github.ApiUrl, github.WriteToken);
            })
            .AddStandardResilienceHandler()
            .Configure(resilience => resilience.Retry.DisableForUnsafeHttpMethods());

        // Transient for the same reason as IPullRequestSource.
        services.AddTransient<IReviewPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value.Source switch
            {
                PullRequestSourceKind.GitHub => serviceProvider.GetRequiredService<GitHubReviewPublisher>(),
                _ => serviceProvider.GetRequiredService<SavedReviewPublisher>(),
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

        // The "Agent" section of the configuration, bound and checked at startup like the "PullRequests" section.
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
