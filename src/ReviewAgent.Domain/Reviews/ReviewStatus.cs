namespace ReviewAgent.Domain.Reviews;

/// <summary>Where a review run is in its lifecycle.</summary>
public enum ReviewStatus
{
    /// <summary>The agent is working: calling tools and proposing comments.</summary>
    Running,

    /// <summary>The agent has finished; a developer decides on each proposed comment.</summary>
    AwaitingApproval,

    /// <summary>The approved comments have been published.</summary>
    Published,

    /// <summary>The run stopped with an error.</summary>
    Failed,
}
