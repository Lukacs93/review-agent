namespace ReviewAgent.Application.SourceControl;

/// <summary>
/// A pull request as the agent sees it: what it is about and which files it changes. The head commit SHA is the
/// latest commit of the pull request, which the review comments are published on; <see langword="null"/> when the
/// source does not tell it, as a saved pull request may not.
/// </summary>
public sealed record PullRequestSnapshot(
    string Title,
    string Description,
    string BaseBranch,
    string HeadBranch,
    IReadOnlyList<ChangedFile> Files,
    string? HeadCommitSha = null);
