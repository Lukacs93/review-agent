using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Commands.FailReview;
using ReviewAgent.Application.Reviews.Commands.FinishReview;
using ReviewAgent.Application.Reviews.Commands.FinishVerification;
using ReviewAgent.Application.Reviews.Commands.ProposeComment;
using ReviewAgent.Application.Reviews.Commands.RecordToolCall;
using ReviewAgent.Application.Reviews.Commands.RejectComment;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.Errors;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.Tests.Persistence;

namespace ReviewAgent.Infrastructure.Tests.Agents;

public sealed class ReviewAgentRunnerTests : IDisposable
{
    private const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    private const string Patch = "@@ -8,2 +10,3 @@\n public sealed class OrdersController(\n+    OrdersDbContext db)\n {";
    // The code of the diff's lines 10 and 11 (see Patch).
    private const string LineTenCode = "public sealed class OrdersController(";
    private const string LineElevenCode = "OrdersDbContext db)";
    private const string CommentBody = "Use an application handler instead of the DbContext.";
    private const string Summary = "Adds a discount endpoint. The controller uses the DbContext directly.";
    private const string RulesPath = ".review/rules.md";
    private const string VerifierSummary = "Kept 1 comment and rejected 1.";
    private const string RejectionReason = "Line 10 only declares the class; the DbContext is on line 11.";

    private readonly SqliteTestDatabase _database = new();
    private readonly EfReviewRunRepository _repository;

    public ReviewAgentRunnerTests() => _repository = new EfReviewRunRepository(_database.CreateDbContext());

    [Fact]
    public async Task Proposed_comments_and_summary_finish_the_run()
    {
        var model = new ScriptedChatClient(
            ScriptedChatClient.CallsTool("get_pull_request"),
            ScriptedChatClient.CallsTool(
                ProposeCommentTool.Name,
                new()
                {
                    ["path"] = FilePath,
                    ["line"] = 11,
                    ["lineCode"] = LineElevenCode,
                    ["ruleId"] = "ARCH-01",
                    ["body"] = CommentBody,
                }),
            ScriptedChatClient.Answers(Summary));
        var runId = await StartRunAsync();

        await Runner(() => model).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(Summary, run.Summary);
        Assert.Equal(2, run.ToolCallCount);
        Assert.Equal(TestData.CommitSha, run.ReviewedCommitSha);
        var comment = Assert.Single(run.Comments);
        Assert.Equal(new CodeLocation(FilePath, 11), comment.Location);
        Assert.Equal(new RuleId("ARCH-01"), comment.Rule);
        Assert.Equal(Severity.Error, comment.Severity);
    }

    [Fact]
    public async Task Comment_the_tool_does_not_accept_is_not_saved()
    {
        var model = new ScriptedChatClient(
            ScriptedChatClient.CallsTool(
                ProposeCommentTool.Name,
                new()
                {
                    ["path"] = FilePath,
                    ["line"] = 99,
                    ["lineCode"] = "Not in the diff.",
                    ["ruleId"] = "ARCH-01",
                    ["body"] = CommentBody,
                }),
            ScriptedChatClient.Answers(Summary));
        var runId = await StartRunAsync();

        await Runner(() => model).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(1, run.ToolCallCount);
        Assert.Empty(run.Comments);
    }

    [Fact]
    public async Task Missing_api_key_fails_the_run()
    {
        var runId = await StartRunAsync();

        await Runner(() => throw new InvalidOperationException(AgentErrors.ApiKeyMissing))
            .ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal(AgentErrors.Failed(AgentErrors.ApiKeyMissing), run.FailureReason);
    }

    [Fact]
    public async Task Answer_without_a_summary_fails_the_run()
    {
        var model = new ScriptedChatClient(ScriptedChatClient.Answers(" "));
        var runId = await StartRunAsync();

        await Runner(() => model).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal(AgentErrors.NoSummary, run.FailureReason);
    }

    [Fact]
    public async Task Repository_without_team_rules_fails_the_run()
    {
        var model = new ScriptedChatClient(ScriptedChatClient.Answers(Summary));
        var runId = await StartRunAsync();

        await Runner(() => model, rulesMarkdown: null).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.Failed, run.Status);
        Assert.Equal(AgentErrors.Failed(AgentErrors.TeamRulesNotFound(RulesPath)), run.FailureReason);
    }

    [Fact]
    public async Task Run_that_is_no_longer_running_is_left_alone()
    {
        var runId = await StartRunAsync();
        await new FailReviewCommandHandler(_repository, TimeProvider.System)
            .HandleAsync(new FailReviewCommand(runId, "Stopped."), CancellationToken.None);

        await Runner(() => throw new InvalidOperationException("The model must not be called."))
            .ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal("Stopped.", run.FailureReason);
    }

    [Fact]
    public async Task Verifier_rejects_the_comment_the_code_does_not_support()
    {
        var model = new ScriptedChatClient(
            ProposesComment(line: 10, LineTenCode),
            ProposesComment(line: 11, LineElevenCode),
            ScriptedChatClient.Answers(Summary),
            ScriptedChatClient.CallsToolWith(
                RejectCommentTool.Name,
                messages => new() { ["commentId"] = FirstCommentId(messages), ["reason"] = RejectionReason }),
            ScriptedChatClient.Answers(VerifierSummary));
        var runId = await StartRunAsync();

        await Runner(() => model, verifierEnabled: true).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(Summary, run.Summary);
        Assert.Equal(3, run.ToolCallCount);
        var rejected = Assert.Single(run.Comments, comment => comment.IsRejected);
        Assert.Equal(10, rejected.Location.Line);
        Assert.Equal(RejectionReason, rejected.RejectionReason);
        Assert.Equal(VerifierSummary, run.VerifierSummary);
    }

    [Fact]
    public async Task Verifier_does_not_run_without_proposed_comments()
    {
        var model = new ScriptedChatClient(ScriptedChatClient.Answers(Summary));
        var runId = await StartRunAsync();

        await Runner(() => model, verifierEnabled: true).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(Summary, run.Summary);
        Assert.Null(run.VerifierSummary);
    }

    [Fact]
    public async Task Verifier_turned_off_does_not_run()
    {
        var model = new ScriptedChatClient(
            ProposesComment(line: 11, LineElevenCode),
            ScriptedChatClient.Answers(Summary));
        var runId = await StartRunAsync();

        await Runner(() => model, verifierEnabled: false).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(Summary, run.Summary);
        Assert.DoesNotContain(run.Comments, comment => comment.IsRejected);
        Assert.Null(run.VerifierSummary);
    }

    [Fact]
    public async Task Failed_verification_keeps_the_comments_and_says_so()
    {
        const string error = "The model is not available.";
        var model = new ScriptedChatClient(
            ProposesComment(line: 11, LineElevenCode),
            ScriptedChatClient.Answers(Summary),
            ScriptedChatClient.Fails(new InvalidOperationException(error)));
        var runId = await StartRunAsync();

        await Runner(() => model, verifierEnabled: true).ReviewAsync(runId, CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal($"{Summary}\n\n{AgentErrors.VerificationFailed(error)}", run.Summary);
        Assert.False(Assert.Single(run.Comments).IsRejected);
        Assert.Null(run.VerifierSummary);
    }

    public void Dispose() => _database.Dispose();

    private static Func<IReadOnlyList<ChatMessage>, ChatResponse> ProposesComment(int line, string lineCode) =>
        ScriptedChatClient.CallsTool(
            ProposeCommentTool.Name,
            new()
            {
                ["path"] = FilePath,
                ["line"] = line,
                ["lineCode"] = lineCode,
                ["ruleId"] = "ARCH-01",
                ["body"] = CommentBody,
            });

    // The verifier's task lists each proposed comment as "- id: <id>"; the first one is the first proposed.
    private static string FirstCommentId(IReadOnlyList<ChatMessage> messages)
    {
        const string idLabel = "- id: ";
        var task = messages.Last(message => message.Role == ChatRole.User).Text;
        var idStart = task.IndexOf(idLabel, StringComparison.Ordinal) + idLabel.Length;
        var idEnd = task.IndexOf('\n', idStart);
        return task[idStart..idEnd];
    }

    private ReviewAgentRunner Runner(
        Func<IChatClient> createModel,
        string? rulesMarkdown = TestData.RulesMarkdown,
        bool verifierEnabled = false) =>
        new(
            _repository,
            new FakePullRequestSource(TestData.PullRequest(FilePath, Patch), rulesMarkdown),
            new InMemoryToolsConnector(),
            new FakeChatClientFactory(createModel),
            new ReviewAgentCommands(
                new RecordToolCallCommandHandler(_repository, TimeProvider.System),
                new ProposeCommentCommandHandler(_repository, TimeProvider.System),
                new RejectCommentCommandHandler(_repository, TimeProvider.System),
                new FinishVerificationCommandHandler(_repository, TimeProvider.System),
                new FinishReviewCommandHandler(_repository, TimeProvider.System),
                new FailReviewCommandHandler(_repository, TimeProvider.System)),
            Options.Create(new AgentOptions
            {
                Endpoint = new Uri("https://model.example/"),
                Model = "test-model",
                McpServerUrl = new Uri("http://localhost:5090/"),
                TeamRulesPath = RulesPath,
                MaxToolRounds = 10,
                Timeout = TimeSpan.FromMinutes(1),
                Verifier = new VerifierOptions { Enabled = verifierEnabled },
            }),
            NullLoggerFactory.Instance);

    private async Task<Guid> StartRunAsync()
    {
        var run = ReviewRun.Start(ReviewRunId.New(), new PullRequestRef("test", "demo", 1), DateTimeOffset.UtcNow);
        await _repository.SaveAsync(run, CancellationToken.None);
        return run.Id.Value;
    }

    private async Task<ReviewRun> LoadAsync(Guid runId) =>
        await _repository.GetAsync(new ReviewRunId(runId), CancellationToken.None);

    private sealed class FakePullRequestSource(PullRequestSnapshot snapshot, string? rulesMarkdown)
        : IPullRequestSource
    {
        public Task<PullRequestSnapshot> GetPullRequestAsync(
            PullRequestRef pullRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public Task<string?> GetFileAsync(PullRequestRef pullRequest, string path, CancellationToken cancellationToken) =>
            Task.FromResult(path == RulesPath ? rulesMarkdown : null);
    }

    // Stands in for the MCP server: one read tool, enough to see that tool calls are recorded.
    private sealed class InMemoryToolsConnector : IPullRequestToolsConnector
    {
        public Task<PullRequestToolsConnection> ConnectAsync(
            PullRequestRef pullRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PullRequestToolsConnection(
                [AIFunctionFactory.Create(() => "Title: Add order discounts", "get_pull_request")],
                connection: null));
    }

    private sealed class FakeChatClientFactory(Func<IChatClient> create) : IAgentChatClientFactory
    {
        public IChatClient Create() => create();
    }
}
