using ReviewAgent.Domain.Rules.Errors;

namespace ReviewAgent.Domain.Rules;

/// <summary>
/// The rules a team reviews its code against. Used to check that a rule the agent refers to exists,
/// and to take its severity from the team's definition instead of from the model.
/// </summary>
public sealed class RuleSet
{
    private readonly Dictionary<RuleId, Rule> _rulesById;

    public RuleSet(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        Rules = rules.ToList();

        if (Rules.Count == 0)
        {
            throw new DomainException(RuleErrors.EmptyRuleSet);
        }

        _rulesById = new Dictionary<RuleId, Rule>();
        foreach (var rule in Rules)
        {
            if (!_rulesById.TryAdd(rule.Id, rule))
            {
                throw new DomainException(RuleErrors.DuplicateRule(rule.Id));
            }
        }
    }

    /// <summary>The rules in the order they are defined.</summary>
    public IReadOnlyList<Rule> Rules { get; }

    public IReadOnlyList<RuleId> Ids => Rules.Select(rule => rule.Id).ToList();

    public Rule? Find(RuleId ruleId) => _rulesById.GetValueOrDefault(ruleId);

    public bool Contains(RuleId ruleId) => _rulesById.ContainsKey(ruleId);
}
