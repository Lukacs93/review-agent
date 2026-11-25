using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FailReview;

/// <summary>The review stopped with an error, and what the error came from.</summary>
public sealed record FailReviewCommand(Guid RunId, string Reason, ReviewFailureKind Kind);
