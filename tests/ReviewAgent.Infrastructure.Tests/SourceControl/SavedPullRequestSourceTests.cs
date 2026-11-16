using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.Saved;
using ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class SavedPullRequestSourceTests : IDisposable
{
    private const string RepositoryOwner = "test";
    private const string RepositoryName = "demo";
    private const int PullRequestNumber = 1;
    private const string PullRequestFolder = "test/demo/1";
    private const string ChangedFilePath = "src/Order.cs";
    private const string ChangedFileContent = "public sealed class Order;";

    private const string PullRequestJson = """
        {
          "title": "Add discounts",
          "description": "Adds a discount endpoint.",
          "baseBranch": "main",
          "headBranch": "feature/discounts",
          "files": [
            { "path": "src/Order.cs", "status": "Modified", "patch": "@@ -1 +1 @@\n-old\n+new" }
          ]
        }
        """;

    private readonly TemporaryDirectory _root = new();

    public SavedPullRequestSourceTests()
    {
        _root.WriteFile($"{PullRequestFolder}/pull-request.json", PullRequestJson);
        _root.WriteFile($"{PullRequestFolder}/repository/{ChangedFilePath}", ChangedFileContent);
    }

    private static PullRequestRef PullRequest => new(RepositoryOwner, RepositoryName, PullRequestNumber);

    private SavedPullRequestSource Source => new(_root.Path);

    [Fact]
    public async Task Pull_request_is_read_from_its_folder()
    {
        var snapshot = await Source.GetPullRequestAsync(PullRequest, CancellationToken.None);

        Assert.Equal("Add discounts", snapshot.Title);
        Assert.Equal("Adds a discount endpoint.", snapshot.Description);
        Assert.Equal("main", snapshot.BaseBranch);
        Assert.Equal("feature/discounts", snapshot.HeadBranch);
        var file = Assert.Single(snapshot.Files);
        Assert.Equal(ChangedFilePath, file.Path);
        Assert.Equal(FileChangeStatus.Modified, file.Status);
        Assert.Equal("@@ -1 +1 @@\n-old\n+new", file.Patch);
    }

    [Fact]
    public async Task Unknown_pull_request_is_not_found()
    {
        var unknown = new PullRequestRef(RepositoryOwner, RepositoryName, 2);

        var error = await Assert.ThrowsAsync<PullRequestNotFoundException>(
            () => Source.GetPullRequestAsync(unknown, CancellationToken.None));

        Assert.Equal(SourceControlErrors.PullRequestNotFound(unknown), error.Message);
    }

    [Fact]
    public async Task File_is_read_from_the_saved_repository()
    {
        var content = await Source.GetFileAsync(PullRequest, ChangedFilePath, CancellationToken.None);

        Assert.Equal(ChangedFileContent, content);
    }

    [Fact]
    public async Task Missing_file_returns_null()
    {
        var content = await Source.GetFileAsync(PullRequest, "src/Missing.cs", CancellationToken.None);

        Assert.Null(content);
    }

    [Theory]
    [InlineData("../pull-request.json")]
    [InlineData("../../../../outside.txt")]
    [InlineData("src/../../pull-request.json")]
    public async Task Path_outside_the_repository_is_rejected(string path)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => Source.GetFileAsync(PullRequest, path, CancellationToken.None));

        Assert.StartsWith(SavedPullRequestErrors.PathOutsideRepository(path), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Absolute_path_is_rejected()
    {
        var path = Path.Combine(_root.Path, PullRequestFolder, "pull-request.json");

        await Assert.ThrowsAsync<ArgumentException>(
            () => Source.GetFileAsync(PullRequest, path, CancellationToken.None));
    }

    public void Dispose() => _root.Dispose();
}
