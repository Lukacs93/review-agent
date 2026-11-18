namespace ReviewAgent.Application.Reviews.Commands.FailInterruptedReviews;

/// <summary>
/// Fails the reviews that were still running when the application stopped. Run once at startup, before any new
/// review can start: the agent's work on them was lost, and what it saved so far may be incomplete.
/// </summary>
/// <remarks>
/// It has no data: it always fails every running review, with the same reason. The type is still needed, because
/// it names the operation, and dependency injection finds its handler by it.
/// </remarks>
public sealed record FailInterruptedReviewsCommand;
