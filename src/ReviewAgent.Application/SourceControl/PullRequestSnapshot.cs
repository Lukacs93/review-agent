namespace ReviewAgent.Application.SourceControl;

/// <summary>A pull request as the agent sees it: what it is about and which files it changes.</summary>
public sealed record PullRequestSnapshot(
    string Title,
    string Description,
    string BaseBranch,
    string HeadBranch,
    IReadOnlyList<ChangedFile> Files);
