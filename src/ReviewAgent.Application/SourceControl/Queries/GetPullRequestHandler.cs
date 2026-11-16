using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.SourceControl.Queries;

/// <summary>
/// Reads a pull request from the configured source (saved or GitHub).
/// Throws <see cref="PullRequestNotFoundException"/> if it does not exist.
/// </summary>
public sealed class GetPullRequestHandler(IPullRequestSource source)
    : IQueryHandler<GetPullRequest, PullRequestSnapshot>
{
    public Task<PullRequestSnapshot> HandleAsync(GetPullRequest query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pullRequest = new PullRequestRef(query.RepositoryOwner, query.RepositoryName, query.PullRequestNumber);
        return source.GetPullRequestAsync(pullRequest, cancellationToken);
    }
}
