namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>The <c>PullRequests:GitHub</c> section of the configuration.</summary>
internal sealed class GitHubOptions
{
    /// <summary>The address of the GitHub REST API; public GitHub by default.</summary>
    // Change it only for GitHub Enterprise Server.
    public Uri ApiUrl { get; init; } = new("https://api.github.com/");

    /// <summary>The GitHub personal access token the requests are sent with.</summary>
    // Fine-grained token: read access to pull requests and contents for reviewing, read and write access to pull
    // requests for publishing. Optional for reviewing public repositories, but without it GitHub allows only 60
    // requests an hour; publishing always needs it. A secret: keep it in user secrets (development), an environment
    // variable or a Kubernetes secret, never in appsettings.json.
    public string? Token { get; init; }
}
