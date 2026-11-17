namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>The verifier agent rejects a proposed comment, with the reason.</summary>
public sealed record RejectComment(Guid RunId, Guid CommentId, string Reason);
