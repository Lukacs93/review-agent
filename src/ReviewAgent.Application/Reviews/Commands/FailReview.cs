namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>The review stopped with an error, for example because the model could not be reached.</summary>
public sealed record FailReview(Guid RunId, string Reason);
