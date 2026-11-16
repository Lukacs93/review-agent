using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Errors;

/// <summary>The messages of the exceptions thrown when loading or saving review runs.</summary>
public static class ReviewRunRepositoryErrors
{
    public static string ConcurrentChange(ReviewRunId runId) =>
        $"Review run {runId} was changed by another save since it was loaded. Load it again and retry.";

    public static string RunNotFound(ReviewRunId runId) => $"Review run {runId} was not found.";
}
