namespace ReviewAgent.Evaluation.Dataset;

/// <summary>
/// One task of the dataset: a pull request made of a commit of the demo repository and a patch, and the comments
/// its review should get.
/// </summary>
/// <param name="Repository">The GitHub repository the base commit is in, as <c>owner/name</c>.</param>
/// <param name="HeadBranch">The pull request's branch name, neutral so it tells nothing about the task.</param>
/// <param name="Patch">The pull request's changes, as <c>git diff</c> writes them.</param>
internal sealed record EvaluationTask(
    string Id,
    TaskGroup Group,
    string Repository,
    string BaseCommit,
    string HeadBranch,
    string Title,
    string Description,
    string Patch,
    IReadOnlyList<ExpectedFinding> Expected)
{
    /// <summary>
    /// A hash of the task's record. A result made for another version of the task (a corrected expected line, a
    /// changed patch) is not reused when a measurement continues.
    /// </summary>
    public string Fingerprint { get; init; } = string.Empty;
}
