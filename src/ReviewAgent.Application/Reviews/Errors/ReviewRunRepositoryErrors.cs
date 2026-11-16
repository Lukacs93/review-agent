using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Errors;

/// <summary>The messages of the exceptions thrown by <see cref="IReviewRunRepository"/> implementations.</summary>
public static class ReviewRunRepositoryErrors
{
    public static string ConcurrentChange(ReviewRunId runId) =>
        $"Review run {runId} was changed by another save since it was loaded. Load it again and retry.";
}
