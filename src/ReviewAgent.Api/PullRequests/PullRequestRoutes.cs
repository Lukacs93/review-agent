namespace ReviewAgent.Api.PullRequests;

/// <summary>
/// The URL paths of the pull request endpoints. Part of the API's public contract, like the review routes.
/// </summary>
internal static class PullRequestRoutes
{
    public const string PullRequests = "/api/pull-requests";

    public const string GetPullRequest = "/{repositoryOwner}/{repositoryName}/{pullRequestNumber:int}";
}
