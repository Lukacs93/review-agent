namespace ReviewAgent.Domain.Rules.Errors;

/// <summary>The messages of the <see cref="DomainException"/>s thrown by the team rules.</summary>
public static class RuleErrors
{
    public const string RuleIdRequired = "Rule ID is required.";
    public const string RuleTitleRequired = "Rule title is required.";
    public const string EmptyRuleSet = "A rule set needs at least one rule.";

    public static string DuplicateRule(RuleId ruleId) => $"Rule {ruleId} is defined more than once.";
}
