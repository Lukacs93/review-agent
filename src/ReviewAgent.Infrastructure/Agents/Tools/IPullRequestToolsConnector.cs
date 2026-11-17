using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>
/// Connects to the tools that read the pull request under review, and returns them for one review.
/// </summary>
internal interface IPullRequestToolsConnector
{
    Task<PullRequestToolsConnection> ConnectAsync(PullRequestRef pullRequest, CancellationToken cancellationToken);
}
