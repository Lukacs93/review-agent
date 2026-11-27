using System.Globalization;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Evaluation.Dataset;
using ReviewAgent.Evaluation.Pulls;

namespace ReviewAgent.Evaluation.Tests.Pulls;

/// <summary>
/// Assembles tasks from a git repository made on the spot in a temporary folder, so the tests need no network.
/// Needs git on the PATH.
/// </summary>
public sealed class PullRequestAssemblerTests : IAsyncLifetime
{
    private const string Repository = "owner/demo";
    private const string ControllerFile = "src/OrdersController.cs";
    private const string AddedCode = "OrdersDbContext db) : ControllerBase";

    // The added line; the file's first lines are outside the diff.
    private const int AddedLine = 11;

    private const string Rules =
        "# Team rules\n\n"
        + "### ARCH-01 – Controllers do not access the database directly (error)\n\n"
        + "Classes in `Controllers/` may only use application-layer handlers.\n";

    private const string Header = "// Orders API\n// The endpoints of the orders service.\n//\n//\n//\n//\n//\n//\n";

    private const string BaseController =
        Header
        + "public sealed class OrdersController(\n"
        + "    PlaceOrderHandler placeOrder) : ControllerBase\n"
        + "{\n"
        + "}\n";

    private const string ChangedController =
        Header
        + "public sealed class OrdersController(\n"
        + "    PlaceOrderHandler placeOrder,\n"
        + "    OrdersDbContext db) : ControllerBase\n"
        + "{\n"
        + "}\n";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "review-agent-evaluation-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private string _baseCommit = string.Empty;
    private string _patch = string.Empty;

    private string RepositoryDirectory => Path.Combine(_root, "owner", "demo");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(RepositoryDirectory, "src"));
        Directory.CreateDirectory(Path.Combine(RepositoryDirectory, ".review"));
        await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "init", "--quiet", "--initial-branch", "main");
        await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(RepositoryDirectory, ".review", "rules.md"), Rules);
        await File.WriteAllTextAsync(Path.Combine(RepositoryDirectory, ControllerFile), BaseController);
        await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "add", "--all");
        await Git.RunAsync(
            RepositoryDirectory,
            CancellationToken.None,
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.com",
            "commit",
            "--quiet",
            "--message",
            "Add the orders controller");
        _baseCommit = (await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "rev-parse", "HEAD")).Trim();

        await File.WriteAllTextAsync(Path.Combine(RepositoryDirectory, ControllerFile), ChangedController);
        _patch = await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "diff");
        await Git.RunAsync(RepositoryDirectory, CancellationToken.None, "checkout", "--quiet", "--", ".");
    }

    public Task DisposeAsync()
    {
        TemporaryFolder.Delete(_root);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pull_request_is_saved_in_the_layout_the_api_reads()
    {
        var assembled = await AssembleAsync(TaskWith(Finding(AddedLine, AddedCode)));

        Assert.Empty(assembled.Problems);
        var pullRequest = Assert.Single(assembled.PullRequests);
        var entries = Directory.EnumerateFileSystemEntries(pullRequest.Directory).Select(Path.GetFileName).ToList();
        Assert.Equal(3, entries.Count);
        Assert.Contains(PullRequestAssembler.PullRequestFileName, entries);
        Assert.Contains(PullRequestAssembler.RepositoryDirectoryName, entries);
        Assert.Contains(PullRequestAssembler.BaseDirectoryName, entries);

        Assert.Equal(
            ChangedController,
            await File.ReadAllTextAsync(Path.Combine(pullRequest.RepositoryDirectory, ControllerFile)));
        Assert.Equal(
            Rules,
            await File.ReadAllTextAsync(Path.Combine(
                pullRequest.Directory,
                PullRequestAssembler.BaseDirectoryName,
                PullRequestAssembler.RulesPath)));
    }

    [Fact]
    public async Task Changed_file_holds_only_the_changes_of_the_diff()
    {
        var assembled = await AssembleAsync(TaskWith(Finding(AddedLine, AddedCode)));

        var file = Assert.Single(Assert.Single(assembled.PullRequests).Files);
        Assert.Equal(ControllerFile, file.Path);
        Assert.Equal(FileChangeStatus.Modified, file.Status);
        Assert.StartsWith("@@", file.Patch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expected_code_that_differs_from_the_line_is_reported()
    {
        var assembled = await AssembleAsync(TaskWith(Finding(AddedLine, "OrdersDbContext context) : ControllerBase")));

        Assert.Equal(
            EvaluationErrors.CodeDiffers(
                "task-01",
                ControllerFile,
                AddedLine,
                "OrdersDbContext context) : ControllerBase",
                AddedCode),
            Assert.Single(assembled.Problems));
    }

    [Fact]
    public async Task Expected_line_outside_the_diff_is_reported()
    {
        var assembled = await AssembleAsync(TaskWith(Finding(1, "// Orders API")));

        Assert.Equal(
            EvaluationErrors.LineNotInDiff("task-01", ControllerFile, 1),
            Assert.Single(assembled.Problems));
    }

    [Fact]
    public async Task Rule_missing_from_the_team_rules_is_reported()
    {
        var assembled = await AssembleAsync(
            TaskWith(Finding(AddedLine, AddedCode) with { Rule = "SEC-02" }));

        Assert.Equal(EvaluationErrors.UnknownRule("task-01", "SEC-02"), Assert.Single(assembled.Problems));
    }

    [Fact]
    public async Task Positive_task_without_a_finding_is_reported()
    {
        var assembled = await AssembleAsync(TaskWith());

        Assert.Equal(
            EvaluationErrors.PositiveTaskNeedsOneFinding("task-01", 0),
            Assert.Single(assembled.Problems));
    }

    [Fact]
    public async Task Patch_that_does_not_apply_is_reported()
    {
        var patchOfAnotherFile = _patch.Replace("PlaceOrderHandler", "OrderHandler", StringComparison.Ordinal);
        var task = TaskWith(Finding(AddedLine, AddedCode)) with { Patch = patchOfAnotherFile };

        var assembled = await AssembleAsync(task);

        Assert.StartsWith(
            "task-01: the pull request could not be assembled",
            Assert.Single(assembled.Problems),
            StringComparison.Ordinal);
        Assert.Empty(assembled.PullRequests);
    }

    private Task<AssembledTasks> AssembleAsync(EvaluationTask task) =>
        new PullRequestAssembler(_root + Path.DirectorySeparatorChar)
            .AssembleAsync([task], Path.Combine(_root, "work"), CancellationToken.None);

    private EvaluationTask TaskWith(params ExpectedFinding[] expected) =>
        new(
            "task-01",
            TaskGroup.Positive,
            Repository,
            _baseCommit,
            "feature/task-01",
            "Add an order endpoint",
            "Adds an endpoint.",
            _patch,
            expected);

    private static ExpectedFinding Finding(int line, string code) =>
        new("ARCH-01", ControllerFile, [new ExpectedLine(line, code)], Optional: false, Reason: null);
}
