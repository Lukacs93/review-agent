namespace ReviewAgent.Application.Reviews.Commands.RejectComment;

/// <summary>The verifier agent rejects a proposed comment, with the reason.</summary>
public sealed record RejectCommentCommand(Guid RunId, Guid CommentId, string Reason);
