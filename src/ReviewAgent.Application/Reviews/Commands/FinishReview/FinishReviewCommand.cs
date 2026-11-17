namespace ReviewAgent.Application.Reviews.Commands.FinishReview;

/// <summary>The agent finished the review; the comments now wait for the developer's decisions.</summary>
public sealed record FinishReviewCommand(Guid RunId, string Summary);
