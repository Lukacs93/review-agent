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
/// Runs the real API in memory against its own data directory and SQLite database file, which is migrated at
/// startup like in production; both are deleted when the tests finish. Started reviews are not queued for the agent, and runs are not
/// queued for publishing.
/// </summary>
public sealed class ReviewAgentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "review-agent-tests-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Not the API's own "data" folder, so the tests leave nothing in the project. The relative database file
        // goes into this directory.
        builder.UseSetting("Storage:DataDirectory", _dataDirectory);
        builder.UseSetting(
            "ConnectionStrings:" + DependencyInjection.ConnectionStringName,
            "Data Source=review-agent.db");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReviewScheduler>();
            services.AddSingleton<IReviewScheduler, NoReviewScheduler>();
            services.RemoveAll<IPublishingScheduler>();
            services.AddSingleton<IPublishingScheduler, NoPublishingScheduler>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDataDirectory();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDataDirectory();
    }

    private void DeleteDataDirectory()
    {
        // SQLite keeps pooled connections open, which would lock the file on Windows.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
