using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Reviews.Queries;

/// <summary>A proposed comment and the developer's decision on it, as plain data.</summary>
public sealed record ReviewCommentDetails(
    Guid Id,
    string FilePath,
    int Line,
    string RuleId,
    Severity Severity,
    string ProposedBody,
    string Body,
    string? SuggestedFix,
    CommentDecision Decision,
    bool IsPublished)
{
    public static ReviewCommentDetails From(ReviewComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return new ReviewCommentDetails(
            comment.Id.Value,
            comment.Location.FilePath,
            comment.Location.Line,
            comment.Rule.Value,
            comment.Severity,
            comment.ProposedBody,
            comment.Body,
            comment.SuggestedFix,
            comment.Decision,
            comment.IsPublished);
    }
}
