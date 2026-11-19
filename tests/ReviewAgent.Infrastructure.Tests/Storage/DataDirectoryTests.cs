using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.Persistence;

namespace ReviewAgent.Infrastructure.Tests.Storage;

public sealed class DataDirectoryTests : IDisposable
{
    private readonly TemporaryDirectory _contentRoot = new();

    private string DataDirectory => Path.Combine(_contentRoot.Path, "data");

    [Fact]
    public void Relative_database_file_is_in_the_data_directory()
    {
        using var services = BuildServices("Data Source=review-agent.db");
        using var scope = services.CreateScope();

        var connectionString = scope.ServiceProvider.GetRequiredService<ReviewAgentDbContext>()
            .Database.GetConnectionString();

        Assert.Equal("Data Source=" + Path.Combine(DataDirectory, "review-agent.db"), connectionString);
    }

    [Fact]
    public void Full_database_path_is_kept()
    {
        var file = Path.Combine(_contentRoot.Path, "elsewhere", "review-agent.db");
        using var services = BuildServices("Data Source=" + file);
        using var scope = services.CreateScope();

        var connectionString = scope.ServiceProvider.GetRequiredService<ReviewAgentDbContext>()
            .Database.GetConnectionString();

        Assert.Equal("Data Source=" + file, connectionString);
    }

    [Fact]
    public async Task Saved_reviews_are_published_into_the_data_directory()
    {
        using var services = BuildServices("Data Source=review-agent.db");
        var publisher = services.GetRequiredService<IReviewPublisher>();
        var runId = Guid.NewGuid();
        var publication = new ReviewPublication(runId, new PullRequestRef("test", "demo", 1), null, "Summary.", []);

        await publisher.PublishAsync(publication, CancellationToken.None);

        Assert.True(File.Exists(
            Path.Combine(DataDirectory, "published-reviews", "test", "demo", "1", $"{runId}.json")));
    }

    public void Dispose() => _contentRoot.Dispose();

    private ServiceProvider BuildServices(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:" + Infrastructure.DependencyInjection.ConnectionStringName] = connectionString,
                ["Storage:DataDirectory"] = "data",
            })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(_contentRoot.Path))
            .AddInfrastructure()
            .BuildServiceProvider();
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "ReviewAgent.Api";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
