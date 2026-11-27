using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Agent;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Application.Reviews.Queries.GetRunLog;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.Errors;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Logging;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Persistence.Errors;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.Persistence.RunLog;
using ReviewAgent.Infrastructure.SourceControl;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;
using ReviewAgent.Infrastructure.SourceControl.Saved;
using ReviewAgent.Infrastructure.Storage;

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
    /// The name of the agents' OpenTelemetry source: the review runs, the model calls and the tool calls are traced
    /// and measured under it.
    /// </summary>
    public const string TelemetrySourceName = "ReviewAgent.Agents";

    // SQLite's name for a database kept in memory instead of a file.
    private const string InMemoryDataSource = ":memory:";

    /// <summary>
    /// Everything the API needs from the Infrastructure layer: the database, the pull request sources, the review
    /// publishers and the review agent.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) =>
        services.AddPersistence().AddSourceControl().AddReviewPublishing().AddReviewAgent().AddRunLog();

    /// <summary>The event store: the EF Core context and the review run repository.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The connection string is read when a context is first created, not here, so a value configured after
        // this registration still applies.
        services.AddStorage();

        services.AddDbContext<ReviewAgentDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(DatabaseErrors.ConnectionStringMissing(ConnectionStringName));

            options.UseSqlite(InDataDirectory(connectionString, serviceProvider));
        });

        services.AddScoped<IReviewRunRepository, EfReviewRunRepository>();
        services.AddScoped<IRunLogReader, EfRunLogReader>();

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
    /// The review publishers: to GitHub, or to the data directory for saved pull requests. The same
    /// <c>PullRequests:Source</c> setting as the sources' decides which one <see cref="IReviewPublisher"/>
    /// resolves to, so call it after <see cref="AddSourceControl"/>. Only the API publishes.
    /// </summary>
    public static IServiceCollection AddReviewPublishing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddStorage();

        // Publishing to GitHub needs the write token, so its absence stops the application at startup instead of
        // failing the first publishing. Checked here, not with the other settings, because the MCP server reads the
        // same settings and never gets the write token.
        services.AddOptions<PullRequestSourceOptions>()
            .Validate(
                options => options.Source != PullRequestSourceKind.GitHub
                    || !string.IsNullOrWhiteSpace(options.GitHub.WriteToken),
                GitHubErrors.WriteTokenRequired)
            .ValidateOnStart();

        // A singleton for the same reason as the saved source. It writes into the data directory, never next to the
        // saved pull requests, which are copied into the application's output folder by the build.
        services.AddSingleton(serviceProvider => new SavedReviewPublisher(
            Path.Combine(DataDirectoryPath.Resolve(serviceProvider), SavedReviewPublisher.DirectoryName),
            serviceProvider.GetRequiredService<ILogger<SavedReviewPublisher>>()));

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

        services.AddSingleton(serviceProvider => new ModelRateLimiter(
            serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value.RequestsPerMinute
                ?? throw new InvalidOperationException(AgentErrors.RequestsPerMinuteNotSet),
            TimeSpan.FromMinutes(1),
            serviceProvider.GetRequiredService<TimeProvider>(),
            serviceProvider.GetRequiredService<ILogger<ModelRateLimiter>>()));

        // The model's requests: the same standard resilience handler as GitHub's (retries with backoff on 429, 503
        // and timeouts, honouring Retry-After, and a circuit breaker), with timeouts long enough for a model.
        // HttpClient's own 100-second timeout would cut through the retries, so the handler's timeouts replace it.
        // With Agent:RequestsPerMinute, the handler's rate limiter, the first of its steps, holds a request back until
        // the service allows it, so the wait is not counted against the request's timeouts.
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

                if (agent.RequestsPerMinute is not null)
                {
                    var limiter = serviceProvider.GetRequiredService<ModelRateLimiter>();
                    resilience.RateLimiter.RateLimiter = arguments =>
                        limiter.AcquireAsync(arguments.Context.CancellationToken);
                }
            });

        services.AddSingleton<IAgentChatClientFactory, OpenAICompatibleChatClientFactory>();
        services.AddSingleton<IPullRequestToolsConnector, McpPullRequestToolsConnector>();
        services.AddScoped<ReviewAgentCommands>();
        services.AddScoped<IReviewAgent, ReviewAgentRunner>();

        return services;
    }

    /// <summary>
    /// Saves the log lines written while a review run is running, with the run's ID, so the UI can show a run's log.
    /// </summary>
    public static IServiceCollection AddRunLog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RunLogOptions>()
            .BindConfiguration(RunLogOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The provider stamps the lines with the time. The Application layer registers the same clock; TryAdd keeps
        // the Infrastructure usable without it (the tests build only this layer) and adds no second clock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RunLogQueue>();
        services.AddSingleton<ILoggerProvider, RunLogProvider>();
        services.AddHostedService<RunLogWriter>();

        return services;
    }

    /// <summary>The "Storage" section of the configuration, bound and checked at startup.</summary>
    // Both the database and the publishers need it; registering it twice only binds the same values twice.
    private static void AddStorage(this IServiceCollection services) =>
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

    /// <summary>
    /// Moves a relative SQLite database file into the data directory, so it is in the same place wherever the
    /// application was started from. A full path, or an in-memory database, is left as it is.
    /// </summary>
    private static string InDataDirectory(string connectionString, IServiceProvider serviceProvider)
    {
        // The SQLite driver's own builder reads and writes the connection string.
        var connection = new SqliteConnectionStringBuilder(connectionString);
        var file = connection.DataSource;
        var isRelativeFile = !string.IsNullOrEmpty(file)
            && !string.Equals(file, InMemoryDataSource, StringComparison.Ordinal)
            && !Path.IsPathRooted(file);
        if (isRelativeFile)
        {
            connection.DataSource = Path.Combine(DataDirectoryPath.Resolve(serviceProvider), file);
        }

        return connection.ToString();
    }
}
