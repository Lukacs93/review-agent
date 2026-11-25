namespace ReviewAgent.Application.Reviews.Commands.EditComment;

/// <summary>
/// A developer approves a proposed comment with a rewritten body. The replacement code: <see langword="null"/>
/// keeps the proposed one, an empty one removes it, any other replaces it.
/// </summary>
public sealed record EditCommentCommand(Guid RunId, Guid CommentId, string Body, string? ReplacementCode);
