using Microsoft.Extensions.AI;

namespace ReviewAgent.Infrastructure.Agents.Tools;

/// <summary>The tools for one review, and the connection behind them, closed when the review ends.</summary>
internal sealed class PullRequestToolsConnection(IReadOnlyList<AIFunction> tools, IAsyncDisposable? connection)
    : IAsyncDisposable
{
    public IReadOnlyList<AIFunction> Tools { get; } = tools;

    public ValueTask DisposeAsync() => connection?.DisposeAsync() ?? ValueTask.CompletedTask;
}
