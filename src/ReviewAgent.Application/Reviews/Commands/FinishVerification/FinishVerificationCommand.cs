namespace ReviewAgent.Application.Reviews.Commands.FinishVerification;

/// <summary>The verifier agent finished double-checking the comments, with its closing sentence.</summary>
public sealed record FinishVerificationCommand(Guid RunId, string Summary);
