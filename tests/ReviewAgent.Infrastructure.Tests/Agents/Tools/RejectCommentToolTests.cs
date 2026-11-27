using Microsoft.Extensions.AI;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.RejectComment;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Tests.Agents.Tools;

public sealed class RejectCommentToolTests
{
    private const string Reason = "The controller only declares the class on this line.";

    private static readonly Guid _runId = Guid.NewGuid();
    private static readonly Guid _commentId = Guid.NewGuid();

    private readonly RecordingRejectComment _handler = new();

    private RejectCommentTool Tool => new(_runId, [_commentId], _handler);

    [Fact]
    public async Task Proposed_comment_is_rejected_with_the_reason()
    {
        var result = await Tool.RejectAsync(_commentId.ToString(), Reason);

        Assert.Equal("Comment rejected.", result);
        Assert.Equal(new RejectCommentCommand(_runId, _commentId, Reason), Assert.Single(_handler.Commands));
    }

    [Theory]
    [InlineData("not-an-id")]
    [InlineData("4f8c2a1e-0000-0000-0000-000000000000")]
    public async Task Comment_not_in_the_list_is_not_rejected(string commentId)
    {
        var result = await Tool.RejectAsync(commentId, Reason);

        Assert.Equal(RejectCommentErrors.UnknownComment(commentId), result);
        Assert.Empty(_handler.Commands);
    }

    // The model's call misses a required argument: it gets what is wrong instead of a failed call.
    [Fact]
    public async Task Call_without_a_required_argument_is_answered_with_what_is_missing()
    {
        var result = await Tool.AsAIFunction().InvokeAsync(new AIFunctionArguments { ["reason"] = Reason });

        var answer = Assert.IsType<string>(result);
        Assert.StartsWith("The call was not run", answer, StringComparison.Ordinal);
        Assert.Contains("commentId", answer, StringComparison.Ordinal);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_without_a_reason_is_not_rejected()
    {
        var result = await Tool.RejectAsync(_commentId.ToString(), " ");

        Assert.Equal(RejectCommentErrors.ReasonRequired, result);
        Assert.Empty(_handler.Commands);
    }

    [Fact]
    public async Task Comment_is_rejected_once()
    {
        var tool = Tool;
        var commentId = _commentId.ToString();
        await tool.RejectAsync(commentId, Reason);

        var result = await tool.RejectAsync(commentId, "Said differently.");

        Assert.Equal(RejectCommentErrors.AlreadyRejected(commentId), result);
        Assert.Single(_handler.Commands);
    }

    private sealed class RecordingRejectComment : ICommandHandler<RejectCommentCommand>
    {
        public List<RejectCommentCommand> Commands { get; } = [];

        public Task HandleAsync(RejectCommentCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
