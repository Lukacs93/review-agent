using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Integration.Tests.SourceControl;

/// <summary>
/// Checks the sample pull request the application ships with: the build copies it next to the application,
/// and the configured source reads it.
/// </summary>
public sealed class SavedPullRequestSampleTests(ReviewAgentApiFactory factory) : IClassFixture<ReviewAgentApiFactory>
{
    private static PullRequestRef SamplePullRequest => new("Lukacs93", "review-agent-demo", 1);

    [Fact]
    public async Task Sample_pull_request_is_available()
    {
        var source = factory.Services.GetRequiredService<IPullRequestSource>();

        var snapshot = await source.GetPullRequestAsync(SamplePullRequest, CancellationToken.None);

        string[] expectedFiles =
        [
            "src/Orders.Api/Application/Basket.cs",
            "src/Orders.Api/Controllers/OrdersController.cs",
            "src/Orders.Api/Domain/Order.cs",
        ];
        Assert.Equal(expectedFiles, snapshot.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task Team_rules_are_in_the_sample_repository()
    {
        var source = factory.Services.GetRequiredService<IPullRequestSource>();

        var rules = await source.GetFileAsync(SamplePullRequest, ".review/rules.md", CancellationToken.None);

        Assert.NotNull(rules);
        Assert.Contains("### ARCH-01", rules, StringComparison.Ordinal);
    }
}
