using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Reviews.Queries.GetReviewRun;

/// <summary>
/// A proposed comment and the developer's decision on it, as plain data. Every value is set by name, so values of
/// the same type (the file path, the rule, the two bodies) cannot be swapped.
/// </summary>
public sealed record ReviewCommentDetails
{
    public required Guid Id { get; init; }

    public required string FilePath { get; init; }

    public required int Line { get; init; }

    public required string RuleId { get; init; }

    public required Severity Severity { get; init; }

    /// <summary>The text as the agent proposed it.</summary>
    public required string ProposedBody { get; init; }

    /// <summary>The text that will be published: the proposed text, or the developer's edit.</summary>
    public required string Body { get; init; }

    /// <summary>The fix as free text, from comments proposed before <see cref="ReplacementCode"/>.</summary>
    public string? SuggestedFix { get; init; }

    /// <summary>The code the agent proposed to replace the comment's line with.</summary>
    public string? ProposedReplacementCode { get; init; }

    /// <summary>
    /// The code that will be published as a suggested change: the proposed code, or the developer's edit.
    /// </summary>
    public string? ReplacementCode { get; init; }

    public required CommentDecision Decision { get; init; }

    public required bool IsPublished { get; init; }

    /// <summary>Why the verifier agent rejected the comment; such a comment needs no decision.</summary>
    public string? RejectionReason { get; init; }

    public static ReviewCommentDetails From(ReviewComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return new ReviewCommentDetails
        {
            Id = comment.Id.Value,
            FilePath = comment.Location.FilePath,
            Line = comment.Location.Line,
            RuleId = comment.Rule.Value,
            Severity = comment.Severity,
            ProposedBody = comment.ProposedBody,
            Body = comment.Body,
            SuggestedFix = comment.SuggestedFix,
            ProposedReplacementCode = comment.ProposedReplacementCode,
            ReplacementCode = comment.ReplacementCode,
            Decision = comment.Decision,
            IsPublished = comment.IsPublished,
            RejectionReason = comment.RejectionReason,
        };
    }
}
