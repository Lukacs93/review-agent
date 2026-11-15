namespace ReviewAgent.Domain.Reviews;

/// <summary>The developer's decision on a proposed comment.</summary>
public enum CommentDecision
{
    /// <summary>No decision yet.</summary>
    Pending,

    /// <summary>Approved as proposed.</summary>
    Approved,

    /// <summary>Approved with a rewritten body.</summary>
    Edited,

    /// <summary>Will not be published.</summary>
    Discarded,
}
