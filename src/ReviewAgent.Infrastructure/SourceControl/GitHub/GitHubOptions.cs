namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>The <c>PullRequests:GitHub</c> section of the configuration.</summary>
internal sealed class GitHubOptions
{
    /// <summary>
    /// The address of the GitHub REST API. The default is public GitHub; set <c>PullRequests:GitHub:ApiUrl</c> only
    /// for GitHub Enterprise Server.
    /// </summary>
    public Uri ApiUrl { get; init; } = new("https://api.github.com/");

    /// <summary>
    /// A fine-grained personal access token with read-only access to pull requests and contents. Optional for public
    /// repositories, but without it GitHub allows only 60 requests an hour. A secret: keep it out of
    /// <c>appsettings.json</c> (user secrets in development, an environment variable or a Kubernetes secret
    /// elsewhere).
    /// </summary>
    public string? Token { get; init; }
}
