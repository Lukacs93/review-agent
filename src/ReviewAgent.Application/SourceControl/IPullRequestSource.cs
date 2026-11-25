using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.SourceControl;

/// <summary>
/// Reads the pull request under review, from the source the configuration chooses: a copy saved to disk or a
/// hosting service such as GitHub. Read-only: nothing here can change the repository.
/// </summary>
public interface IPullRequestSource
{
    /// <summary>
    /// The pull request's title, description and changed files.
    /// Throws <see cref="PullRequestNotFoundException"/> if it does not exist.
    /// </summary>
    Task<PullRequestSnapshot> GetPullRequestAsync(PullRequestRef pullRequest, CancellationToken cancellationToken);

    /// <summary>
    /// The content of a file in the given version of the repository, or <see langword="null"/> if there is no such
    /// file. The path is relative to the repository root.
    /// </summary>
    Task<string?> GetFileAsync(
        PullRequestRef pullRequest,
        RepositoryVersion version,
        string path,
        CancellationToken cancellationToken);
}
