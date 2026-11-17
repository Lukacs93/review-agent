namespace ReviewAgent.Application.Reviews.Commands.FailReview;

/// <summary>The review stopped with an error, for example because the model could not be reached.</summary>
public sealed record FailReviewCommand(Guid RunId, string Reason);
