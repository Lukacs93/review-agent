using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Persistence.Errors;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.SourceControl.Saved;
using ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

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
    /// The setting that holds the directory of the saved pull requests. A relative path is relative to the
    /// application's own directory, where the build copies the <c>samples</c> folder.
    /// </summary>
    public const string SavedPullRequestsDirectoryKey = "PullRequests:SavedDirectory";

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

        services.AddSingleton<IPullRequestSource>(serviceProvider =>
        {
            var directory = serviceProvider.GetRequiredService<IConfiguration>()[SavedPullRequestsDirectoryKey]
                ?? throw new InvalidOperationException(
                    SavedPullRequestErrors.DirectoryNotConfigured(SavedPullRequestsDirectoryKey));

            return new SavedPullRequestSource(Path.GetFullPath(directory, AppContext.BaseDirectory));
        });

        return services;
    }
}
