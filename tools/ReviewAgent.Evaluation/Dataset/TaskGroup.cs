namespace ReviewAgent.Evaluation.Dataset;

/// <summary>What kind of pull request a task is.</summary>
internal enum TaskGroup
{
    /// <summary>Breaks exactly one rule, planted on purpose.</summary>
    Positive,

    /// <summary>Looks like it breaks a rule but does not: any comment on it is false.</summary>
    Negative,

    /// <summary>A real pull request of the demo repository, or a clean one.</summary>
    Real,
}
