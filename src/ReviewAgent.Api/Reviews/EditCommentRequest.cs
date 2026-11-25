namespace ReviewAgent.Api.Reviews;

/// <summary>
/// The body of <c>POST /api/reviews/{runId}/comments/{commentId}/edit</c>: the comment's text as it is approved.
/// The replacement code: left out keeps the proposed one, an empty one removes it, any other replaces it.
/// </summary>
internal sealed record EditCommentRequest(string Body, string? ReplacementCode = null);
