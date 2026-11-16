namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>A developer discards a proposed comment.</summary>
public sealed record DiscardComment(Guid RunId, Guid CommentId);
