namespace ReviewAgent.Api.Reviews;

/// <summary>The body of <c>POST /api/reviews</c>.</summary>
internal sealed record StartReviewRequest(string RepositoryOwner, string RepositoryName, int PullRequestNumber);
