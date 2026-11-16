namespace ReviewAgent.Domain.Rules;

/// <summary>How serious a rule violation is, as defined in the team's rules.</summary>
public enum Severity
{
    /// <summary>Must be fixed before the pull request is merged.</summary>
    Error,

    /// <summary>Should be fixed, but does not block the merge.</summary>
    Warning,
}
