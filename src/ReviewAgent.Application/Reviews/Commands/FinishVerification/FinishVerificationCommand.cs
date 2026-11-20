namespace ReviewAgent.Application.Reviews.Commands.FinishVerification;

/// <summary>The verifier agent finished double-checking the comments.</summary>
public sealed record FinishVerificationCommand(Guid RunId);
