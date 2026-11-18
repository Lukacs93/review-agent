namespace ReviewAgent.Application.Reviews.Commands.PublishReview;

/// <summary>Publishes a review the developer asked to publish. Run by the publishing worker.</summary>
public sealed record PublishReviewCommand(Guid RunId);
