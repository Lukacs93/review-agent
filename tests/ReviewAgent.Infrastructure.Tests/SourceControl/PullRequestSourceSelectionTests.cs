using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.Saved;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class PullRequestSourceSelectionTests
{
    private const string OrderContentPath = "/repos/test/demo/contents/src/Order.cs?ref=refs/pull/1/head";

    [Fact]
    public void Saved_pull_requests_are_the_default()
    {
        using var services = BuildServices(source: null).BuildServiceProvider();

        Assert.IsType<SavedPullRequestSource>(services.GetRequiredService<IPullRequestSource>());
    }

    [Fact]
    public void GitHub_is_used_when_configured()
    {
        using var services = BuildServices(source: "GitHub").BuildServiceProvider();

        Assert.IsType<GitHubPullRequestSource>(services.GetRequiredService<IPullRequestSource>());
    }

    [Fact]
    public async Task Transient_github_failures_are_retried()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Respond(OrderContentPath, HttpStatusCode.ServiceUnavailable);
        handler.Respond(OrderContentPath, HttpStatusCode.OK, "public sealed class Order;");

        var collection = BuildServices(source: "GitHub");
        collection.AddHttpClient<GitHubPullRequestSource>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var services = collection.BuildServiceProvider();
        var source = services.GetRequiredService<IPullRequestSource>();

        var content = await source.GetFileAsync(
            new PullRequestRef("test", "demo", 1),
            "src/Order.cs",
            CancellationToken.None);

        Assert.Equal("public sealed class Order;", content);
        Assert.Equal(2, handler.Requests.Count);
    }

    private static ServiceCollection BuildServices(string? source)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PullRequests:Source"] = source,
                ["PullRequests:SavedDirectory"] = "samples/pull-requests",

                // The API does not start with the GitHub source without it.
                ["PullRequests:GitHub:WriteToken"] = "write-token",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        return services;
    }
}
