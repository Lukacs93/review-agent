using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.SourceControl.Errors;

/// <summary>
/// The messages of the exceptions thrown by <see cref="IPullRequestSource"/> implementations and when reading their
/// diffs.
/// </summary>
public static class SourceControlErrors
{
    public const string UnreadableDiff =
        "A file's diff has no block header (\"@@ -a,b +c,d @@\"), so its line numbers cannot be read. "
        + "The pull request source returned a diff in an unexpected format.";

    public const string TeamRulesPathRequired =
        "PullRequests:TeamRulesPath is required: where the team keeps its rules in the repository.";

    public static string PullRequestNotFound(PullRequestRef pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return $"Pull request {pullRequest} was not found.";
    }
}
