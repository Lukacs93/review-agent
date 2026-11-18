namespace ReviewAgent.Infrastructure.Agents;

/// <summary>The <c>Agent:Verifier</c> section of the configuration.</summary>
internal sealed class VerifierOptions
{
    /// <summary>
    /// Whether the verifier agent double-checks the proposed comments before the review finishes. It can be turned
    /// off to measure what it adds.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
