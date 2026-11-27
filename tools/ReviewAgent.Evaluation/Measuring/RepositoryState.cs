using ReviewAgent.Evaluation.Pulls;

namespace ReviewAgent.Evaluation.Measuring;

/// <summary>Which version of review-agent a measurement runs: its code, prompts, tasks and expected results.</summary>
internal static class RepositoryState
{
    /// <summary>Marks a commit whose working tree has uncommitted changes, which change the results too.</summary>
    public const string ModifiedSuffix = "+modified";

    private const string SolutionFileName = "ReviewAgent.slnx";

    public static async Task<string> CommitAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var commit = (await Git.RunAsync(repositoryRoot, cancellationToken, "rev-parse", "HEAD")).Trim();
        var changes = await Git.RunAsync(repositoryRoot, cancellationToken, "status", "--porcelain");
        return changes.Length == 0 ? commit : commit + ModifiedSuffix;
    }

    /// <summary>The repository's root: the nearest folder upwards that holds the solution file.</summary>
    public static string FindRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(EvaluationErrors.RepositoryRootNotFound(startDirectory));
    }
}
