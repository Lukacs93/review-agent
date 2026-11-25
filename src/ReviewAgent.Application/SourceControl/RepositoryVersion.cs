namespace ReviewAgent.Application.SourceControl;

/// <summary>Which version of the reviewed repository a file is read from.</summary>
public enum RepositoryVersion
{
    /// <summary>The pull request's latest commit: the code under review.</summary>
    PullRequest,

    /// <summary>
    /// The latest commit of the branch the pull request merges into. The team's rules are read from here, so a
    /// pull request cannot change the rules it is reviewed by, and a merged change applies to every review.
    /// </summary>
    Base,
}
