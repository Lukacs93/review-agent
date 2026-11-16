namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

// The parts of the GitHub REST API answers the source uses; the API sends many more fields.
// JSON names match these properties without regard to case ("filename" -> FileName).

/// <summary><c>GET /repos/{owner}/{repo}/pulls/{number}</c>.</summary>
internal sealed record GitHubPullRequest(string Title, string? Body, GitHubBranch Base, GitHubBranch Head);

internal sealed record GitHubBranch(string Ref);

/// <summary>One item of <c>GET /repos/{owner}/{repo}/pulls/{number}/files</c>.</summary>
internal sealed record GitHubChangedFile(string FileName, string Status, string? Patch);
