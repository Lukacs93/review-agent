namespace ReviewAgent.Api.Errors;

/// <summary>The titles of the problem details answers. The detail is always the exception's own message.</summary>
internal static class ProblemTitles
{
    public const string RuleViolation = "The request breaks a review rule.";
    public const string ReviewRunNotFound = "The review run was not found.";
    public const string ConcurrentChange = "The review run was changed by another request.";
}
