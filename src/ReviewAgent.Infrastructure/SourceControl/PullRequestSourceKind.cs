namespace ReviewAgent.Infrastructure.SourceControl;

/// <summary>Where the reviewed pull requests come from.</summary>
internal enum PullRequestSourceKind
{
    /// <summary>Pull requests saved to disk: no GitHub account needed, and the same input on every run.</summary>
    Saved,

    /// <summary>Live pull requests through the GitHub REST API.</summary>
    GitHub,
}
