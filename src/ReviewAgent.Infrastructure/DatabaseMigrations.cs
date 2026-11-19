using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReviewAgent.Infrastructure.Persistence;
using ReviewAgent.Infrastructure.Storage;

namespace ReviewAgent.Infrastructure;

/// <summary>Brings the database up to date at startup. The context is internal, so this is the way in.</summary>
public static partial class DatabaseMigrations
{
    /// <summary>
    /// Creates the data directory if needed, logs where it is, and applies the migrations the database does not
    /// have yet. Called once at startup.
    /// </summary>
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        // SQLite creates the database file, but not the directory it is in.
        var dataDirectory = DataDirectoryPath.Resolve(services);
        Directory.CreateDirectory(dataDirectory);
        LogDataDirectory(logger, dataDirectory);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewAgentDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Data directory: {DataDirectory}")]
    private static partial void LogDataDirectory(ILogger logger, string dataDirectory);
}
