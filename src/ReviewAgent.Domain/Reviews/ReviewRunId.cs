using System.Globalization;

namespace ReviewAgent.Domain.Reviews;

/// <summary>Identifies a single run of the review agent on a pull request.</summary>
public readonly record struct ReviewRunId(Guid Value)
{
    public static ReviewRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
