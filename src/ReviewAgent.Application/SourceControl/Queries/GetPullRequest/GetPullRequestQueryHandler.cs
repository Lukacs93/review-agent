using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.SourceControl.Queries.GetPullRequest;

/// <summary>
/// Reads a pull request from the configured source (saved or GitHub).
/// Throws <see cref="PullRequestNotFoundException"/> if it does not exist.
/// </summary>
public sealed class GetPullRequestQueryHandler(IPullRequestSource source)
    : IQueryHandler<GetPullRequestQuery, PullRequestSnapshot>
{
    public Task<PullRequestSnapshot> HandleAsync(GetPullRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pullRequest = new PullRequestRef(query.RepositoryOwner, query.RepositoryName, query.PullRequestNumber);
        return source.GetPullRequestAsync(pullRequest, cancellationToken);
    }
}
