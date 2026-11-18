using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.ProposeComment;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Tests.Agents.Tools;

public sealed class ProposeCommentToolTests
{
    private const string FilePath = "src/Orders.Api/Controllers/OrdersController.cs";
    private const string CommentBody = "Use an application handler instead of the DbContext.";

    // The new file's lines 10-12 are in the diff; line 11 is the added one.
    private const string Patch = "@@ -8,2 +10,3 @@\n public sealed class OrdersController(\n+    OrdersDbContext db)\n {";
    private const string LineElevenCode = "OrdersDbContext db)";

    private static readonly Guid _runId = Guid.NewGuid();
    private static readonly string[] _ruleIds = ["ARCH-01", "TEST-01"];

    private readonly RecordingProposeComment _handler = new();

    private ProposeCommentTool Tool => new(_runId, TestData.PullRequest(FilePath, Patch), TestData.Rules, _handler);

    [Fact]
    public async Task Valid_comment_is_saved_with_the_severity_of_the_rule()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, LineElevenCode, "arch-01", CommentBody, "Inject a handler.");

        Assert.Equal("Comment proposed.", result);
        var command = Assert.Single(_handler.Commands);
        Assert.Equal(
            new ProposeCommentCommand(
                _runId, FilePath, 11, "ARCH-01", Severity.Error, CommentBody, "Inject a handler."),
            command);
    }

    [Fact]
    public async Task Comment_on_a_file_not_changed_is_not_saved()
    {
        const string otherFile = "src/Orders.Api/Program.cs";

        var result = await Tool.ProposeAsync(otherFile, 11, LineElevenCode, "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.FileNotChanged(otherFile), result);
        Assert.Empty(_handler.Commands);
    }

    // A model often gives a line number one off; the quoted code decides.
    [Fact]
    public async Task Comment_is_saved_on_the_line_of_the_quoted_code()
    {
        var result = await Tool.ProposeAsync(FilePath, 12, LineElevenCode, "ARCH-01", CommentBody);

        Assert.Equal("Comment proposed on line 11: the quoted code is on line 11, not 12.", result);
        Assert.Equal(11, Assert.Single(_handler.Commands).Line);
    }

    [Fact]
    public async Task Comment_with_code_not_in_the_diff_is_not_saved()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, "OrdersDbContext context)", "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.CodeNotInDiff(FilePath, "OrdersDbContext context)"), result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_without_the_code_of_the_line_is_not_saved()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, " ", "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.LineCodeRequired, result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Code_on_several_lines_needs_the_right_line_number()
    {
        const string patch = "@@ -0,0 +1,4 @@\n+    return null;\n+}\n+    return null;\n+}";
        var tool = new ProposeCommentTool(_runId, TestData.PullRequest(FilePath, patch), TestData.Rules, _handler);

        var onAnotherLine = await tool.ProposeAsync(FilePath, 2, "return null;", "ARCH-01", CommentBody);
        var onOneOfThem = await tool.ProposeAsync(FilePath, 3, "return null;", "ARCH-01", CommentBody);

        Assert.Equal(ProposeCommentErrors.CodeOnSeveralLines(FilePath, "return null;", [1, 3]), onAnotherLine);
        Assert.Equal("Comment proposed.", onOneOfThem);
        Assert.Equal(3, Assert.Single(_handler.Commands).Line);
    }

    [Theory]
    [InlineData("NAME-99")]
    [InlineData(" ")]
    public async Task Comment_with_an_unknown_rule_is_not_saved(string ruleId)
    {
        var result = await Tool.ProposeAsync(FilePath, 11, LineElevenCode, ruleId, CommentBody);

        Assert.Equal(ProposeCommentErrors.UnknownRule(ruleId, _ruleIds), result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_without_a_body_is_not_saved()
    {
        var result = await Tool.ProposeAsync(FilePath, 11, LineElevenCode, "ARCH-01", " ");

        Assert.Equal(ProposeCommentErrors.BodyRequired, result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Same_comment_is_saved_once()
    {
        var tool = Tool;
        await tool.ProposeAsync(FilePath, 11, LineElevenCode, "ARCH-01", CommentBody);

        var result = await tool.ProposeAsync(FilePath, 11, LineElevenCode, "arch-01", "Said differently.");

        Assert.Equal(ProposeCommentErrors.Duplicate(FilePath, 11, "ARCH-01"), result);
        Assert.Single(_handler.Commands);
    }

    private sealed class RecordingProposeComment : ICommandHandler<ProposeCommentCommand, Guid>
    {
        public List<ProposeCommentCommand> Commands { get; } = [];

        public Task<Guid> HandleAsync(ProposeCommentCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
