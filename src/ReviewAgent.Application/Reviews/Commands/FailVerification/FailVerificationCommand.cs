using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.FailVerification;

/// <summary>The verifier agent could not finish; the review goes on with the comments unchecked.</summary>
public sealed record FailVerificationCommand(Guid RunId, string Reason, ReviewFailureKind Kind);
