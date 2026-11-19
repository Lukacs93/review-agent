using System.Net;
using System.Text.Json;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class GitHubPullRequestSourceTests : IDisposable
{
    private const string Token = "test-token";
    private const string PullRequestPath = "/repos/test/demo/pulls/1";
    private const string FirstFilesPagePath = "/repos/test/demo/pulls/1/files";

    // GitHub's next-page links point to the repository by its ID, not by its name.
    private const string SecondFilesPagePath = "/repositories/42/pulls/1/files?page=2";
    private const string OrderContentPath = "/repos/test/demo/contents/src/Order.cs?ref=refs/pull/1/head";

    private const string PullRequestJson = """
        {
          "title": "Add discounts",
          "body": null,
          "base": { "ref": "main", "sha": "0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e" },
          "head": { "ref": "feature/discounts", "sha": "9fceb02d0ae598e95dc970b74767f19372d61af8" }
        }
        """;

    private const string FilesJson = """
        [
          { "filename": "src/Order.cs", "status": "modified", "patch": "@@ -1 +1 @@\n-old\n+new" },
          { "filename": "src/New.cs", "status": "added", "patch": "@@ -0,0 +1 @@\n+new" },
          { "filename": "src/Old.cs", "status": "removed", "patch": "@@ -1 +0,0 @@\n-old" },
          { "filename": "logo.png", "status": "changed" }
        ]
        """;

    private readonly StubHttpMessageHandler _handler = new();
    private readonly HttpClient _client;

    public GitHubPullRequestSourceTests()
    {
        _client = new HttpClient(_handler);
        GitHubPullRequestSource.ConfigureClient(_client, PublicGitHub, Token);
    }

    private static Uri PublicGitHub => new GitHubOptions().ApiUrl;

    private static PullRequestRef PullRequest => new("test", "demo", 1);

    private GitHubPullRequestSource Source => new(_client);

    [Fact]
    public async Task Pull_request_is_read_from_the_api()
    {
        _handler.Respond(PullRequestPath, HttpStatusCode.OK, PullRequestJson);
        _handler.Respond(FirstFilesPagePath, HttpStatusCode.OK, FilesJson);

        var snapshot = await Source.GetPullRequestAsync(PullRequest, CancellationToken.None);

        Assert.Equal("Add discounts", snapshot.Title);
        Assert.Equal(string.Empty, snapshot.Description);
        Assert.Equal("main", snapshot.BaseBranch);
        Assert.Equal("feature/discounts", snapshot.HeadBranch);
        Assert.Equal("9fceb02d0ae598e95dc970b74767f19372d61af8", snapshot.HeadCommitSha);
        Assert.Collection(
            snapshot.Files,
            file => Assert.Equal(
                new ChangedFile("src/Order.cs", FileChangeStatus.Modified, "@@ -1 +1 @@\n-old\n+new"),
                file),
            file => Assert.Equal(FileChangeStatus.Added, file.Status),
            file => Assert.Equal(FileChangeStatus.Removed, file.Status),
            file => Assert.Equal(new ChangedFile("logo.png", FileChangeStatus.Modified, string.Empty), file));
    }

    // The end of the list comes from the Link header, not from the number of files on a page.
    [Fact]
    public async Task Files_on_several_pages_are_all_read()
    {
        var firstPage = Enumerable.Range(1, 30)
            .Select(index => new { filename = $"src/File{index}.cs", status = "added", patch = "@@" });
        _handler.Respond(PullRequestPath, HttpStatusCode.OK, PullRequestJson);
        _handler.Respond(
            FirstFilesPagePath,
            HttpStatusCode.OK,
            JsonSerializer.Serialize(firstPage),
            nextPage: new Uri("https://api.github.com" + SecondFilesPagePath));
        _handler.Respond(SecondFilesPagePath, HttpStatusCode.OK, FilesJson);

        var snapshot = await Source.GetPullRequestAsync(PullRequest, CancellationToken.None);

        Assert.Equal(34, snapshot.Files.Count);
    }

    // The client sends the token with every request, so it must not follow a link to another host.
    [Fact]
    public async Task Next_page_outside_the_api_is_not_followed()
    {
        var elsewhere = new Uri("https://example.com/files?page=2");
        _handler.Respond(PullRequestPath, HttpStatusCode.OK, PullRequestJson);
        _handler.Respond(FirstFilesPagePath, HttpStatusCode.OK, FilesJson, nextPage: elsewhere);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Source.GetPullRequestAsync(PullRequest, CancellationToken.None));

        Assert.Equal(GitHubErrors.NextPageOutsideApi(elsewhere), error.Message);
        Assert.DoesNotContain(_handler.Requests, request => request.RequestUri!.Host == "example.com");
    }

    [Fact]
    public async Task Unknown_pull_request_is_not_found()
    {
        var error = await Assert.ThrowsAsync<PullRequestNotFoundException>(
            () => Source.GetPullRequestAsync(PullRequest, CancellationToken.None));

        Assert.Equal(SourceControlErrors.PullRequestNotFound(PullRequest), error.Message);
    }

    [Fact]
    public async Task File_is_read_raw_from_the_pull_request_head()
    {
        _handler.Respond(OrderContentPath, HttpStatusCode.OK, "public sealed class Order;");

        var content = await Source.GetFileAsync(PullRequest, "src/Order.cs", CancellationToken.None);

        Assert.Equal("public sealed class Order;", content);
        var request = Assert.Single(_handler.Requests);
        Assert.Contains(request.Headers.Accept, accept => accept.MediaType == "application/vnd.github.raw+json");
    }

    [Fact]
    public async Task Missing_file_returns_null()
    {
        var content = await Source.GetFileAsync(PullRequest, "src/Missing.cs", CancellationToken.None);

        Assert.Null(content);
    }

    [Fact]
    public async Task File_path_is_escaped_in_the_url()
    {
        await Source.GetFileAsync(PullRequest, "src/My File.cs", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(
            "/repos/test/demo/contents/src/My%20File.cs?ref=refs/pull/1/head",
            request.RequestUri!.PathAndQuery);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../user")]
    [InlineData("src/../../user")]
    [InlineData("./src/Order.cs")]
    [InlineData("/src/Order.cs")]
    [InlineData("src//Order.cs")]
    [InlineData("src\\Order.cs")]
    public async Task Invalid_path_is_rejected_without_calling_github(string path)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => Source.GetFileAsync(PullRequest, path, CancellationToken.None));

        Assert.StartsWith(GitHubErrors.InvalidPath(path), error.Message, StringComparison.Ordinal);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Requests_carry_the_token_and_the_required_headers()
    {
        await Source.GetFileAsync(PullRequest, "src/Order.cs", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("Bearer", _client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal(Token, _client.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.NotEmpty(_client.DefaultRequestHeaders.UserAgent);
        Assert.True(_client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"));
        Assert.Equal(
            "https://api.github.com/repos/test/demo/contents/src/Order.cs?ref=refs/pull/1/head",
            request.RequestUri!.ToString());
    }

    [Fact]
    public void Without_a_token_no_authorization_is_sent()
    {
        using var client = new HttpClient();

        GitHubPullRequestSource.ConfigureClient(client, PublicGitHub, token: null);

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Api_url_without_a_trailing_slash_still_works()
    {
        using var client = new HttpClient();

        GitHubPullRequestSource.ConfigureClient(client, new Uri("https://github.example.com/api/v3"), token: null);

        Assert.Equal("https://github.example.com/api/v3/", client.BaseAddress?.ToString());
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}
