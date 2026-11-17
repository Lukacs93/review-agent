namespace ReviewAgent.Application.Reviews.Commands.EditComment;

/// <summary>A developer approves a proposed comment with a rewritten body.</summary>
public sealed record EditCommentCommand(Guid RunId, Guid CommentId, string Body);
