namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>The <c>PullRequests:GitHub</c> section of the configuration.</summary>
internal sealed class GitHubOptions
{
    /// <summary>The address of the GitHub REST API; public GitHub by default.</summary>
    // Change it only for GitHub Enterprise Server.
    public Uri ApiUrl { get; init; } = new("https://api.github.com/");

    /// <summary>The token the pull requests are read with.</summary>
    // Fine-grained token with read access to pull requests and contents. Optional for public repositories, but
    // without it GitHub allows only 60 requests an hour. A secret: keep it in user secrets (development), an
    // environment variable or a Kubernetes secret, never in appsettings.json.
    public string? ReadToken { get; init; }

    /// <summary>The token the reviews are published with; only the publisher gets it.</summary>
    // Fine-grained token with read and write access to pull requests. Kept apart from the read token, so the MCP
    // server, which only reads, never holds a token that can write. A secret, like the read token.
    public string? WriteToken { get; init; }
}
