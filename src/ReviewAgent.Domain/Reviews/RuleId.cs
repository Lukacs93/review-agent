using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Domain.Reviews;

/// <summary>
/// Identifies a team rule, for example <c>ARCH-01</c>.
/// Whether the rule exists is checked against the team's rule set.
/// </summary>
public sealed record RuleId
{
    public RuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(ValueObjectErrors.RuleIdRequired);
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
