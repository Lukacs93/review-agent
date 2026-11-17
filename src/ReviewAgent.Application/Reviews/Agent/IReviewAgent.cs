
namespace ReviewAgent.Application.Reviews.Agent;

/// <summary>
/// Reviews the pull request of a started run: reads it through its tools, proposes comments, then finishes the
/// run with a summary, or fails it with the reason. Every step is saved through the commands.
/// </summary>
public interface IReviewAgent
{
    Task ReviewAsync(Guid runId, CancellationToken cancellationToken);
}
