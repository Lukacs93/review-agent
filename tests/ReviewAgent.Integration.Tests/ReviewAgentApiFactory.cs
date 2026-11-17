using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReviewAgent.Application.Reviews.Scheduling;
using ReviewAgent.Infrastructure;

namespace ReviewAgent.Integration.Tests;

/// <summary>
/// Runs the real API in memory against its own SQLite database file, which is migrated at startup like in
/// production and deleted when the tests finish. Started reviews are not queued for the agent.
/// </summary>
public sealed class ReviewAgentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        "review-agent-tests-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting(
            "ConnectionStrings:" + DependencyInjection.ConnectionStringName,
            "Data Source=" + _databasePath);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReviewScheduler>();
            services.AddSingleton<IReviewScheduler, NoReviewScheduler>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabase();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabase();
    }

    private void DeleteDatabase()
    {
        // SQLite keeps pooled connections open, which would lock the file on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
