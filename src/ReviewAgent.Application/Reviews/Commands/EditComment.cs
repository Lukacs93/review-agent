namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>A developer approves a proposed comment with a rewritten body.</summary>
public sealed record EditComment(Guid RunId, Guid CommentId, string Body);
