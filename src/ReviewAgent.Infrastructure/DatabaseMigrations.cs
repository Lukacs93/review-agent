using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Infrastructure.Persistence;

namespace ReviewAgent.Infrastructure;

/// <summary>Brings the database up to date at startup. The context is internal, so this is the way in.</summary>
public static class DatabaseMigrations
{
    /// <summary>Applies the migrations the database does not have yet. Called once at startup.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewAgentDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
