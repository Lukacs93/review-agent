using System.Globalization;

namespace ReviewAgent.Infrastructure.Tests;

/// <summary>An empty directory for one test, deleted with everything in it when the test ends.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "review-agent-tests-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes a file at a path relative to the directory, creating its folders.</summary>
    public void WriteFile(string relativePath, string content)
    {
        var file = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
