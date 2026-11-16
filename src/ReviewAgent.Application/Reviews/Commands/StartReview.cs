namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>Starts a review run on a pull request.</summary>
public sealed record StartReview(string RepositoryOwner, string RepositoryName, int PullRequestNumber);
