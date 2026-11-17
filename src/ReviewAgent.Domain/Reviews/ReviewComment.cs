using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Domain.Reviews;

/// <summary>A comment proposed by the agent, and the developer's decision on it.</summary>
public sealed class ReviewComment
{
    internal ReviewComment(
        CommentId id,
        CodeLocation location,
        RuleId rule,
        Severity severity,
        string body,
        string? suggestedFix)
    {
        Id = id;
        Location = location;
        Rule = rule;
        Severity = severity;
        ProposedBody = body;
        Body = body;
        SuggestedFix = suggestedFix;
        Decision = CommentDecision.Pending;
    }

    public CommentId Id { get; }

    public CodeLocation Location { get; }

    public RuleId Rule { get; }

    public Severity Severity { get; }

    /// <summary>The text as the agent proposed it.</summary>
    public string ProposedBody { get; }

    /// <summary>The text that will be published: the proposed text, or the developer's edit.</summary>
    public string Body { get; private set; }

    public string? SuggestedFix { get; }

    public CommentDecision Decision { get; private set; }

    public bool IsPublished { get; private set; }

    /// <summary>Why the verifier agent rejected the comment; <see langword="null"/> if it kept it.</summary>
    public string? RejectionReason { get; private set; }

    public bool IsRejected => RejectionReason is not null;

    /// <summary>A rejected comment needs no decision from the developer.</summary>
    public bool IsDecided => Decision != CommentDecision.Pending || IsRejected;

    public bool ShouldBePublished => Decision is CommentDecision.Approved or CommentDecision.Edited;

    internal void Approve()
    {
        Decision = CommentDecision.Approved;
        Body = ProposedBody;
    }

    internal void Edit(string body)
    {
        Decision = CommentDecision.Edited;
        Body = body;
    }

    internal void Discard() => Decision = CommentDecision.Discarded;

    internal void MarkPublished() => IsPublished = true;

    internal void Reject(string reason) => RejectionReason = reason;
}
