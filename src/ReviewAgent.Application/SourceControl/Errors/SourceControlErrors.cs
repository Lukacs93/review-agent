using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.SourceControl.Errors;

/// <summary>The messages of the exceptions thrown by <see cref="IPullRequestSource"/> implementations.</summary>
public static class SourceControlErrors
{
    public static string PullRequestNotFound(PullRequestRef pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return $"Pull request {pullRequest} was not found.";
    }
}
