using ReviewAgent.Domain.Rules.Errors;

namespace ReviewAgent.Domain.Rules;

/// <summary>A single team rule, as defined in the team's rules file.</summary>
public sealed record Rule
{
    public Rule(RuleId id, string title, Severity severity, string description)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException(RuleErrors.RuleTitleRequired);
        }

        Id = id;
        Title = title.Trim();
        Severity = severity;
        Description = description?.Trim() ?? string.Empty;
    }

    public RuleId Id { get; }

    public string Title { get; }

    public Severity Severity { get; }

    /// <summary>The full text of the rule, as the agent reads it.</summary>
    public string Description { get; }
}
