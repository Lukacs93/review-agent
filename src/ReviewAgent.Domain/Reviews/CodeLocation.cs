using System.Globalization;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Domain.Reviews;

/// <summary>A line in a file of the repository. The path is relative to the repository root.</summary>
public sealed record CodeLocation
{
    public CodeLocation(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new DomainException(ValueObjectErrors.FilePathRequired);
        }

        var normalized = filePath.Trim().Replace('\\', '/');

        if (normalized.StartsWith('/'))
        {
            throw new DomainException(ValueObjectErrors.FilePathNotRelative);
        }

        if (line <= 0)
        {
            throw new DomainException(ValueObjectErrors.LineNotPositive);
        }

        FilePath = normalized;
        Line = line;
    }

    public string FilePath { get; }

    public int Line { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{FilePath}:{Line}");
}
