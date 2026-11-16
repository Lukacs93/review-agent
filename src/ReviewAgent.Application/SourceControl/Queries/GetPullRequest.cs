namespace ReviewAgent.Application.SourceControl.Queries;

/// <summary>Asks for a pull request as the agent will see it: its description and changed files.</summary>
public sealed record GetPullRequest(string RepositoryOwner, string RepositoryName, int PullRequestNumber);
