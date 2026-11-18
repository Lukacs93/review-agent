using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class GitHubReviewPublisherTests : IDisposable
{
    // Reviews are created and listed at the same address.
    private const string ReviewsPath = "/repos/test/demo/pulls/1/reviews";

    // GitHub's next-page links point to the repository by its ID, not by its name.
    private const string SecondReviewsPagePath = "/repositories/42/pulls/1/reviews?page=2";
    private const string CommitSha = "9fceb02d0ae598e95dc970b74767f19372d61af8";

    private static readonly Guid _runId = Guid.Parse("6f1c2a9e-3b4d-4e5f-8a7b-1c2d3e4f5a6b");

    private readonly StubHttpMessageHandler _handler = new();
    private readonly HttpClient _client;

    public GitHubReviewPublisherTests()
    {
        _client = new HttpClient(_handler);
        GitHubPullRequestSource.ConfigureClient(_client, new GitHubOptions { Token = "test-token" });
    }

    private static ReviewPublication Publication => new(
        _runId,
        new PullRequestRef("test", "demo", 1),
        CommitSha,
        "One architecture issue.",
        [new CommentToPublish("src/Order.cs", 12, "ARCH-01", "Use an application handler.", "Inject a handler.")]);

    private static string Marker => GitHubReviewRequest.MarkerOf(_runId);

    private GitHubReviewPublisher Publisher => new(_client, NullLogger<GitHubReviewPublisher>.Instance);

    [Fact]
    public async Task Review_is_created_in_one_request_on_the_reviewed_commit()
    {
        _handler.Respond(ReviewsPath, HttpStatusCode.OK, "{}");

        await Publisher.PublishAsync(Publication, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(ReviewsPath, request.RequestUri!.PathAndQuery);
        using var body = JsonDocument.Parse(Assert.Single(_handler.RequestBodies));
        var review = body.RootElement;
        Assert.Equal(CommitSha, review.GetProperty("commit_id").GetString());
        Assert.Equal("COMMENT", review.GetProperty("event").GetString());
        Assert.Equal($"One architecture issue.\n\n{Marker}", review.GetProperty("body").GetString());
        var comment = Assert.Single(review.GetProperty("comments").EnumerateArray().ToList());
        Assert.Equal("src/Order.cs", comment.GetProperty("path").GetString());
        Assert.Equal(12, comment.GetProperty("line").GetInt32());
        Assert.Equal("RIGHT", comment.GetProperty("side").GetString());
        Assert.Equal(
            "**ARCH-01**: Use an application handler.\n\nSuggested fix: Inject a handler.",
            comment.GetProperty("body").GetString());
    }

    // The marker is stored data: the reviews already published carry this exact form.
    [Fact]
    public void Marker_keeps_its_published_form()
    {
        Assert.Equal("<!-- review-agent:run=6f1c2a9e-3b4d-4e5f-8a7b-1c2d3e4f5a6b -->", Marker);
    }

    [Fact]
    public async Task Review_without_a_known_commit_leaves_the_commit_to_GitHub()
    {
        _handler.Respond(ReviewsPath, HttpStatusCode.OK, "{}");

        await Publisher.PublishAsync(Publication with { ReviewedCommitSha = null }, CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(_handler.RequestBodies));
        Assert.False(body.RootElement.TryGetProperty("commit_id", out _));
    }

    [Fact]
    public async Task Failed_publishing_reports_GitHubs_own_message()
    {
        const string answer = """
            {
              "message": "Unprocessable Entity",
              "errors": ["Line could not be resolved", { "resource": "PullRequestReview", "code": "custom" }],
              "documentation_url": "https://docs.github.com/rest/pulls/reviews"
            }
            """;
        _handler.Respond(ReviewsPath, HttpStatusCode.UnprocessableEntity, answer);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Publisher.PublishAsync(Publication, CancellationToken.None));

        Assert.Equal(
            GitHubErrors.RequestFailed(422, "Unprocessable Entity (Line could not be resolved)"),
            error.Message);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, error.StatusCode);
    }

    // For example an HTML page from a proxy: the status is enough here, the whole answer is in the log.
    [Fact]
    public async Task Failed_publishing_without_GitHubs_message_reports_the_status()
    {
        _handler.Respond(ReviewsPath, HttpStatusCode.BadGateway, "<html><body>502 Bad Gateway</body></html>");

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => Publisher.PublishAsync(Publication, CancellationToken.None));

        Assert.Equal(GitHubErrors.RequestFailedWithoutMessage(502), error.Message);
    }

    [Fact]
    public async Task Review_with_the_runs_marker_is_found()
    {
        var reviews = new[] { new { body = "Looks good." }, new { body = $"One architecture issue.\n\n{Marker}" } };
        _handler.Respond(ReviewsPath, HttpStatusCode.OK, JsonSerializer.Serialize(reviews));

        Assert.True(await Publisher.IsPublishedAsync(Publication, CancellationToken.None));
    }

    // The end of the list comes from the Link header, not from the number of reviews on a page.
    [Fact]
    public async Task Reviews_on_several_pages_are_all_searched()
    {
        var firstPage = Enumerable.Range(1, 30).Select(index => new { body = $"Review {index}" });
        var lastPage = new[] { new { body = $"One architecture issue.\n\n{Marker}" } };
        _handler.Respond(
            ReviewsPath,
            HttpStatusCode.OK,
            JsonSerializer.Serialize(firstPage),
            nextPage: new Uri("https://api.github.com" + SecondReviewsPagePath));
        _handler.Respond(SecondReviewsPagePath, HttpStatusCode.OK, JsonSerializer.Serialize(lastPage));

        Assert.True(await Publisher.IsPublishedAsync(Publication, CancellationToken.None));
    }

    [Fact]
    public async Task Pull_request_without_the_marker_has_no_review_of_the_run()
    {
        var reviews = new[] { new { body = (string?)null }, new { body = (string?)"Looks good." } };
        _handler.Respond(ReviewsPath, HttpStatusCode.OK, JsonSerializer.Serialize(reviews));

        Assert.False(await Publisher.IsPublishedAsync(Publication, CancellationToken.None));
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}
