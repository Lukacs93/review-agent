namespace ReviewAgent.Application.Reviews.Commands.RecordModelCall;

/// <summary>
/// An agent called the model once. Recorded so the review shows how long its model calls took and how many tokens
/// they used.
/// </summary>
public sealed record RecordModelCallCommand(
    Guid RunId,
    string AgentName,
    TimeSpan Duration,
    long? InputTokens,
    long? OutputTokens);
