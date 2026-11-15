using System.Globalization;

namespace ReviewAgent.Domain.Reviews;

/// <summary>Identifies a review comment within a review run.</summary>
public readonly record struct CommentId(Guid Value)
{
    public static CommentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
