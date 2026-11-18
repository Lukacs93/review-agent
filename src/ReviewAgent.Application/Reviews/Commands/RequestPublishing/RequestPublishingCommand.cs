namespace ReviewAgent.Application.Reviews.Commands.RequestPublishing;

/// <summary>A developer asks for the approved comments of a review to be published on the pull request.</summary>
public sealed record RequestPublishingCommand(Guid RunId);
