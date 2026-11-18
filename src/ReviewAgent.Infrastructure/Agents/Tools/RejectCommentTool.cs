using System.ComponentModel;
using Microsoft.Extensions.AI;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.RejectComment;
using ReviewAgent.Infrastructure.Agents.Tools.Errors;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// The tool the verifier agent rejects a proposed comment with. It can only reject the comments of this review, once,
/// and with a reason; it cannot add or change comments. A request that fails a check is not saved, and the model
/// gets the reason.
/// </summary>
internal sealed class RejectCommentTool
{
    public const string Name = "reject_comment";

    private const string Description =
        "Rejects a proposed review comment that is wrong: the code does not break the rule, the rule does not cover "
        + "it, or the line does not show the problem. Returns whether the comment was rejected.";

    private readonly Guid _runId;
    private readonly HashSet<Guid> _proposedCommentIds;
    private readonly HashSet<Guid> _rejectedCommentIds = [];
    private readonly ICommandHandler<RejectCommentCommand> _rejectComment;

    public RejectCommentTool(
        Guid runId,
        IEnumerable<Guid> proposedCommentIds,
        ICommandHandler<RejectCommentCommand> rejectComment)
    {
        _runId = runId;
        _proposedCommentIds = [.. proposedCommentIds];
        _rejectComment = rejectComment;
    }

    public AIFunction AsAIFunction() => AIFunctionFactory.Create(RejectAsync, Name, Description);

    public async Task<string> RejectAsync(
        [Description("ID of the proposed comment, as listed in the task.")] string commentId,
        [Description("Why the comment is wrong, in one sentence.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(commentId, out var id) || !_proposedCommentIds.Contains(id))
        {
            return RejectCommentErrors.UnknownComment(commentId);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return RejectCommentErrors.ReasonRequired;
        }

        if (!_rejectedCommentIds.Add(id))
        {
            return RejectCommentErrors.AlreadyRejected(commentId);
        }

        await _rejectComment.HandleAsync(new RejectCommentCommand(_runId, id, reason), cancellationToken);
        return "Comment rejected.";
    }
}
