using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Tests.Agents.Tools;

public sealed class ProposeCommentToolTests
{
    private const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    private const string CommentBody = "Use an application handler instead of the DbContext.";

    // The new file's lines 10-12 are in the diff.
    private const string Patch = "@@ -8,2 +10,3 @@\n public sealed class OrdersController(\n+    OrdersDbContext db)\n {";

    private static readonly Guid _runId = Guid.NewGuid();
    private static readonly string[] _ruleIds = ["ARCH-01", "TEST-01"];

    private readonly RecordingProposeComment _handler = new();

    private ProposeCommentTool Tool => new(_runId, TestData.PullRequest(FilePath, Patch), TestData.Rules, _handler);

    [Fact]
    public async Task Valid_comment_is_saved_with_the_severity_of_the_rule()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, "arch-01", CommentBody, "Inject a handler.");

        Assert.Equal("Comment proposed.", result);
        var command = Assert.Single(_handler.Commands);
        Assert.Equal(
            new ProposeComment(_runId, FilePath, 11, "ARCH-01", Severity.Error, CommentBody, "Inject a handler."),
            command);
    }

    [Fact]
    public async Task Comment_on_a_file_not_changed_is_not_saved()
    {
        const string otherFile = "src/Orders.Api/Program.cs";

        var result = await Tool.ProposeAsync(otherFile, 11, "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.FileNotChanged(otherFile), result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_on_a_line_outside_the_diff_is_not_saved()
    {
        var result = await Tool.ProposeAsync(FilePath, 30, "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.LineNotInDiff(FilePath, 30), result);
        Assert.Empty(_handler.Commands);
    }

    [Theory]
    [InlineData("NAME-99")]
    [InlineData(" ")]
    public async Task Comment_with_an_unknown_rule_is_not_saved(string ruleId)
    {
        var result = await Tool.ProposeAsync(FilePath, 11, ruleId, CommentBody);

        Assert.Equal(ProposeCommentErrors.UnknownRule(ruleId, _ruleIds), result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_without_a_body_is_not_saved()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, "ARCH-01", " ");

        Assert.Equal(ProposeCommentErrors.BodyRequired, result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Same_comment_is_saved_once()
    {
        var tool = Tool;
        await tool.ProposeAsync(FilePath, 11, "ARCH-01", CommentBody);

        var result = await tool.ProposeAsync(FilePath, 11, "arch-01", "Said differently.");

        Assert.Equal(ProposeCommentErrors.Duplicate(FilePath, 11, "ARCH-01"), result);
        Assert.Single(_handler.Commands);
    }

    private sealed class RecordingProposeComment : ICommandHandler<ProposeComment, Guid>
    {
        public List<ProposeComment> Commands { get; } = [];

        public Task<Guid> HandleAsync(ProposeComment command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
