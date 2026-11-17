using ReviewAgent.Domain.Rules;

namespace ReviewAgent.Application.Reviews.Commands;

/// <summary>The agent proposes a review comment on a line of a file, referencing a team rule.</summary>
public sealed record ProposeComment(
    Guid RunId,
    string FilePath,
    int Line,
    string RuleId,
    Severity Severity,
    string Body,
    string? SuggestedFix);
