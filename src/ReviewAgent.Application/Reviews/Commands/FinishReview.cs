namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>The agent finished the review; the comments now wait for the developer's decisions.</summary>
public sealed record FinishReview(Guid RunId, string Summary);
