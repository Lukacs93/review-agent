using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Application.Reviews.Queries.GetRunLog;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Integration.Tests.Reviews;

public sealed class ReviewEndpointsTests(ReviewAgentApiFactory factory) : IClassFixture<ReviewAgentApiFactory>
{
    private const string RepositoryOwner = "test";
    private const string RepositoryName = "demo";
    private const int PullRequestNumber = 1;
    private const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    private const int Line = 12;
    private const string Rule = "ARCH-01";
    private const string CommentBody = "Use an application handler instead of the DbContext.";
    private const string EditedBody = "Move this query to an application handler.";
    private const string Summary = "One architecture issue.";
    private const string CommitSha = "9fceb02d0ae598e95dc970b74767f19372d61af8";

    // The API sends enums as text, so the tests read them the same way.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static Uri ReviewsUri => new("/api/reviews", UriKind.Relative);

    [Fact]
    public async Task Started_review_can_be_read_back()
    {
        using var client = factory.CreateClient();

        using var created = await client.PostAsJsonAsync(
            ReviewsUri,
            new { RepositoryOwner, RepositoryName, PullRequestNumber });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var details = await client.GetFromJsonAsync<ReviewRunDetails>(created.Headers.Location, _jsonOptions);
        Assert.NotNull(details);
        Assert.Equal(ReviewStatus.Running, details.Status);
        Assert.Equal(RepositoryOwner, details.RepositoryOwner);
        Assert.Equal(RepositoryName, details.RepositoryName);
        Assert.Equal(PullRequestNumber, details.PullRequestNumber);
    }

    [Fact]
    public async Task Unknown_review_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(RunUri(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Run_log_of_a_new_run_is_empty()
    {
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(
            ReviewsUri,
            new { RepositoryOwner, RepositoryName, PullRequestNumber });
        var runId = await ReadCreatedIdAsync(created);

        var lines = await client.GetFromJsonAsync<List<RunLogEntryDetails>>(RunLogUri(runId), _jsonOptions);

        Assert.NotNull(lines);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task Run_log_of_an_unknown_run_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(RunLogUri(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_request_returns_problem_details()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            ReviewsUri,
            new { RepositoryOwner = " ", RepositoryName, PullRequestNumber });

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ValueObjectErrors.RepositoryOwnerRequired);
    }

    [Fact]
    public async Task Approved_comment_is_saved()
    {
        var (runId, commentId) = await SaveFinishedRunWithOneCommentAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(CommentUri(runId, commentId, "approve"), content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var comment = await ReadSingleCommentAsync(client, runId);
        Assert.Equal(CommentDecision.Approved, comment.Decision);
    }

    [Fact]
    public async Task Edited_comment_is_saved_with_the_new_body()
    {
        var (runId, commentId) = await SaveFinishedRunWithOneCommentAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            CommentUri(runId, commentId, "edit"),
            new { Body = EditedBody });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var comment = await ReadSingleCommentAsync(client, runId);
        Assert.Equal(CommentDecision.Edited, comment.Decision);
        Assert.Equal(EditedBody, comment.Body);
    }

    [Fact]
    public async Task Discarded_comment_is_saved()
    {
        var (runId, commentId) = await SaveFinishedRunWithOneCommentAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(CommentUri(runId, commentId, "discard"), content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var comment = await ReadSingleCommentAsync(client, runId);
        Assert.Equal(CommentDecision.Discarded, comment.Decision);
    }

    [Fact]
    public async Task Decision_on_unknown_review_returns_not_found()
    {
        var runId = Guid.NewGuid();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(CommentUri(runId, Guid.NewGuid(), "approve"), content: null);

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ReviewRunRepositoryErrors.RunNotFound(new ReviewRunId(runId)));
    }

    [Fact]
    public async Task Decision_before_the_review_finished_returns_bad_request()
    {
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(
            ReviewsUri,
            new { RepositoryOwner, RepositoryName, PullRequestNumber });
        var runId = await ReadCreatedIdAsync(created);

        using var response = await client.PostAsync(CommentUri(runId, Guid.NewGuid(), "approve"), content: null);

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ReviewRunErrors.DecideWhenNotFinished);
    }

    [Fact]
    public async Task Requested_publishing_is_accepted()
    {
        var (runId, commentId) = await SaveFinishedRunWithOneCommentAsync();
        using var client = factory.CreateClient();
        using var approved = await client.PostAsync(CommentUri(runId, commentId, "approve"), content: null);
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);

        using var response = await client.PostAsync(PublishUri(runId), content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var details = await client.GetFromJsonAsync<ReviewRunDetails>(response.Headers.Location, _jsonOptions);
        Assert.NotNull(details);
        Assert.Equal(ReviewStatus.Publishing, details.Status);
    }

    [Fact]
    public async Task Publishing_with_undecided_comments_returns_bad_request()
    {
        var (runId, _) = await SaveFinishedRunWithOneCommentAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(PublishUri(runId), content: null);

        await ProblemDetailsAssert.HasProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ReviewRunErrors.UndecidedComments);
    }

    private static Uri RunUri(Guid runId) =>
        new(string.Create(CultureInfo.InvariantCulture, $"/api/reviews/{runId}"), UriKind.Relative);

    private static Uri RunLogUri(Guid runId) =>
        new(string.Create(CultureInfo.InvariantCulture, $"/api/reviews/{runId}/log"), UriKind.Relative);

    private static Uri PublishUri(Guid runId) =>
        new(string.Create(CultureInfo.InvariantCulture, $"/api/reviews/{runId}/publish"), UriKind.Relative);

    private static Uri CommentUri(Guid runId, Guid commentId, string action) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"/api/reviews/{runId}/comments/{commentId}/{action}"),
            UriKind.Relative);

    private static async Task<Guid> ReadCreatedIdAsync(HttpResponseMessage created)
    {
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<ReviewCommentDetails> ReadSingleCommentAsync(HttpClient client, Guid runId)
    {
        var details = await client.GetFromJsonAsync<ReviewRunDetails>(RunUri(runId), _jsonOptions);
        Assert.NotNull(details);
        return Assert.Single(details.Comments);
    }

    // The agent proposes comments in a later step; until then the tests save a finished run directly.
    private async Task<(Guid RunId, Guid CommentId)> SaveFinishedRunWithOneCommentAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReviewRunRepository>();

        var now = DateTimeOffset.UtcNow;
        var run = ReviewRun.Start(
            ReviewRunId.New(),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            now);
        var commentId = run.ProposeComment(
            new CodeLocation(FilePath, Line),
            new RuleId(Rule),
            Severity.Error,
            CommentBody,
            replacementCode: null,
            now);
        run.Finish(Summary, CommitSha, now);

        await repository.SaveAsync(run, CancellationToken.None);
        return (run.Id.Value, commentId.Value);
    }
}
