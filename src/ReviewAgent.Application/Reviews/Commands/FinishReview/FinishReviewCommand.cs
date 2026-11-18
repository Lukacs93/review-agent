namespace ReviewAgent.Application.Reviews.Commands.FinishReview;

/// <summary>
/// The agent finished the review; the comments now wait for the developer's decisions. The reviewed commit is the
/// pull request's latest commit when the agent read it, if the source tells it.
/// </summary>
public sealed record FinishReviewCommand(Guid RunId, string Summary, string? ReviewedCommitSha);
