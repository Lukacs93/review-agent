using ReviewAgent.Application.Reviews;

namespace ReviewAgent.McpServer.Errors;

/// <summary>
/// The messages the tools return as errors. The model reads them, so they say what went wrong and what to do.
/// </summary>
internal static class ToolErrors
{
    public const string PullRequestHeaderMissing =
        "The pull request under review is not set: the agent must send the "
        + $"{ReviewAgentHeaderNames.ReviewedPullRequest} header as owner/repository/number.";

    public static string PullRequestHeaderInvalid(string value) =>
        $"'{value}' is not a valid pull request in the {ReviewAgentHeaderNames.ReviewedPullRequest} header. "
        + "Expected owner/repository/number.";

    public static string FileNotChanged(string path) =>
        $"'{path}' is not changed in this pull request. Use get_file to read files that are not changed.";

    public static string NoDiff(string path) =>
        $"There is no text diff for '{path}', for example because it is a binary or a very large file.";

    public static string FileNotFound(string path) => $"There is no file '{path}' in the pull request.";

    public static string TeamRulesNotFound(string path) =>
        $"The repository has no team rules file at '{path}'. Review without team rules and say so in the summary.";
}
