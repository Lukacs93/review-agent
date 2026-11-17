namespace ReviewAgent.Application.Reviews.Commands.StartReview;

/// <summary>Starts a review run on a pull request.</summary>
public sealed record StartReviewCommand(string RepositoryOwner, string RepositoryName, int PullRequestNumber);
