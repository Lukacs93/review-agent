using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Integration.Tests.PullRequests;

public sealed class PullRequestEndpointsTests(ReviewAgentApiFactory factory) : IClassFixture<ReviewAgentApiFactory>
{
    // The API sends enums as text, so the tests read them the same way.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Sample_pull_request_is_returned()
    {
        using var client = factory.CreateClient();

        var snapshot = await client.GetFromJsonAsync<PullRequestSnapshot>(
            new Uri("/api/pull-requests/Lukacs93/review-agent-demo/1", UriKind.Relative),
            _jsonOptions);

        Assert.NotNull(snapshot);
        Assert.Equal("Add order discounts and shipping", snapshot.Title);
        Assert.Equal(3, snapshot.Files.Count);
        Assert.Contains(snapshot.Files, file => file.Status == FileChangeStatus.Added);
    }

    [Fact]
    public async Task Unknown_pull_request_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/pull-requests/Lukacs93/review-agent-demo/999", UriKind.Relative));

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.NotFound,
            SourceControlErrors.PullRequestNotFound(new PullRequestRef("Lukacs93", "review-agent-demo", 999)));
    }

    [Fact]
    public async Task Invalid_repository_owner_returns_bad_request()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/pull-requests/-invalid/review-agent-demo/1", UriKind.Relative));

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ValueObjectErrors.RepositoryOwnerInvalid);
    }
}
