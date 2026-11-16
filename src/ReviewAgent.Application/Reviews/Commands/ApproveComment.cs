namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>A developer approves a proposed comment.</summary>
public sealed record ApproveComment(Guid RunId, Guid CommentId);
