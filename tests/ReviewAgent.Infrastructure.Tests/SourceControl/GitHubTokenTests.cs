using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class GitHubTokenTests : IDisposable
{
    private const string ReadToken = "read-token";
    private const string WriteToken = "write-token";
    private const string OrderContentPath = "/repos/test/demo/contents/src/Order.cs?ref=refs/pull/1/head";
    private const string ReviewsPath = "/repos/test/demo/pulls/1/reviews";

    private static readonly PullRequestRef _pullRequest = new("test", "demo", 1);

    private readonly StubHttpMessageHandler _handler = new();
    private readonly ServiceProvider _services;

    public GitHubTokenTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PullRequests:Source"] = "GitHub",
                ["PullRequests:GitHub:ReadToken"] = ReadToken,
                ["PullRequests:GitHub:WriteToken"] = WriteToken,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        services.AddHttpClient<GitHubPullRequestSource>().ConfigurePrimaryHttpMessageHandler(() => _handler);
        services.AddHttpClient<GitHubReviewPublisher>().ConfigurePrimaryHttpMessageHandler(() => _handler);
        _services = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pull_requests_are_read_with_the_read_token()
    {
        _handler.Respond(OrderContentPath, HttpStatusCode.OK, "public sealed class Order;");
        var source = _services.GetRequiredService<GitHubPullRequestSource>();

        await source.GetFileAsync(_pullRequest, "src/Order.cs", CancellationToken.None);

        Assert.Equal(ReadToken, Assert.Single(_handler.Requests).Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Reviews_are_published_with_the_write_token()
    {
        _handler.Respond(ReviewsPath, HttpStatusCode.OK, "[]");
        var publisher = _services.GetRequiredService<IReviewPublisher>();
        var publication = new ReviewPublication(Guid.NewGuid(), _pullRequest, null, "Summary.", []);

        await publisher.IsPublishedAsync(publication, CancellationToken.None);

        Assert.Equal(WriteToken, Assert.Single(_handler.Requests).Headers.Authorization?.Parameter);
    }

    [Fact]
    public void Publishing_to_GitHub_without_a_write_token_is_rejected()
    {
        using var services = BuildServicesWithoutWriteToken(publishing: true);

        var error = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value);

        Assert.Contains(GitHubErrors.WriteTokenRequired, error.Failures);
    }

    [Fact]
    public void Reading_from_GitHub_needs_no_write_token()
    {
        using var services = BuildServicesWithoutWriteToken(publishing: false);

        var options = services.GetRequiredService<IOptions<PullRequestSourceOptions>>().Value;

        Assert.Null(options.GitHub.WriteToken);
    }

    public void Dispose()
    {
        _services.Dispose();
        _handler.Dispose();
    }

    // Like the API (publishing) or the MCP server (reading only), with the GitHub source and only a read token.
    private static ServiceProvider BuildServicesWithoutWriteToken(bool publishing)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PullRequests:Source"] = "GitHub",
                ["PullRequests:GitHub:ReadToken"] = ReadToken,
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSourceControl();
        if (publishing)
        {
            services.AddReviewPublishing();
        }

        return services.BuildServiceProvider();
    }
}
