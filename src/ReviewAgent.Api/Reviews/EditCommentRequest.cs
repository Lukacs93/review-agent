namespace ReviewAgent.Api.Reviews;

/// <summary>The body of <c>POST /api/reviews/{runId}/comments/{commentId}/edit</c>.</summary>
internal sealed record EditCommentRequest(string Body);
