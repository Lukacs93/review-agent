namespace ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

/// <summary>The messages of the exceptions thrown when reading saved pull requests.</summary>
internal static class SavedPullRequestErrors
{
    public static string PathOutsideRepository(string path) =>
        $"'{path}' is outside the repository. Use a path relative to the repository root.";

    public static string EmptyFile(string file) => $"The saved pull request file '{file}' is empty.";

    public static string DirectoryNotConfigured(string key) =>
        $"The saved pull requests directory is not configured. Set {key} in the configuration.";
}
