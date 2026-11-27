using System.Text.Json;

namespace ReviewAgent.Evaluation.Measuring;

/// <summary>
/// What a measurement was started with. A measurement that continues uses the same number of runs and the same
/// settings, and stops when review-agent's commit changed (unless told to go on).
/// </summary>
/// <param name="Commit">review-agent's commit; see <see cref="RepositoryState"/>.</param>
internal sealed record ExecutionRecord(
    string Name,
    DateTimeOffset StartedAt,
    string Commit,
    int Runs,
    AgentSettings Settings)
{
    /// <summary>The folder of the measurements' records, under the results.</summary>
    public const string ExecutionsDirectoryName = "executions";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<ExecutionRecord?> ReadAsync(
        string resultsDirectory,
        string name,
        CancellationToken cancellationToken)
    {
        var file = PathOf(resultsDirectory, name);
        if (!File.Exists(file))
        {
            return null;
        }

        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<ExecutionRecord>(stream, _jsonOptions, cancellationToken);
    }

    public async Task SaveAsync(string resultsDirectory, CancellationToken cancellationToken)
    {
        var file = PathOf(resultsDirectory, Name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await using var stream = File.Create(file);
        await JsonSerializer.SerializeAsync(stream, this, _jsonOptions, cancellationToken);
    }

    private static string PathOf(string resultsDirectory, string name) =>
        Path.Combine(resultsDirectory, ExecutionsDirectoryName, $"{name}.json");
}
