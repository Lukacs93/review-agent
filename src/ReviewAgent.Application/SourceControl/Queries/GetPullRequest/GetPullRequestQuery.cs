namespace ReviewAgent.Application.SourceControl.Queries.GetPullRequest;

/// <summary>Asks for a pull request as the agent will see it: its description and changed files.</summary>
public sealed record GetPullRequestQuery(string RepositoryOwner, string RepositoryName, int PullRequestNumber);
