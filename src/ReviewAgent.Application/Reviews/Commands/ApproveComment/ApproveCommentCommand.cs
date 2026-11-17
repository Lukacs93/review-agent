namespace ReviewAgent.Application.Reviews.Commands.ApproveComment;

/// <summary>A developer approves a proposed comment.</summary>
public sealed record ApproveCommentCommand(Guid RunId, Guid CommentId);
