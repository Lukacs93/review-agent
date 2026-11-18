namespace ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

/// <summary>The messages of the exceptions thrown when reading pull requests from GitHub.</summary>
internal static class GitHubErrors
{
    public static string InvalidPath(string path) =>
        $"'{path}' is not a valid file path. Use a path relative to the repository root, without '.' or '..' parts.";

    public static string NextPageOutsideApi(Uri nextPage) =>
        $"GitHub pointed the next page to '{nextPage}', outside its API address, so it was not followed.";
}
