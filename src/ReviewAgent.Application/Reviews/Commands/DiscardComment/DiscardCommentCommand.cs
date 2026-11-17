namespace ReviewAgent.Application.Reviews.Commands.DiscardComment;

/// <summary>A developer discards a proposed comment.</summary>
public sealed record DiscardCommentCommand(Guid RunId, Guid CommentId);
