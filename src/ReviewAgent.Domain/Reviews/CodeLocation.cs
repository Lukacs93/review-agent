using System.Globalization;

namespace ReviewAgent.Domain.Reviews;

/// <summary>A line in a file of the repository. The path is relative to the repository root.</summary>
public sealed record CodeLocation
{
    public CodeLocation(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new DomainException("File path is required.");
        }

        var normalized = filePath.Trim().Replace('\\', '/');

        if (normalized.StartsWith('/'))
        {
            throw new DomainException("File path must be relative to the repository root.");
        }

        if (line <= 0)
        {
            throw new DomainException("Line number must be positive.");
        }

        FilePath = normalized;
        Line = line;
    }

    public string FilePath { get; }

    public int Line { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{FilePath}:{Line}");
}
