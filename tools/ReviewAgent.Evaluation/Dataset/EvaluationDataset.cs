using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReviewAgent.Evaluation.Dataset;

/// <summary>Reads the dataset: a JSON Lines file, one task per line.</summary>
internal static class EvaluationDataset
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<IReadOnlyList<EvaluationTask>> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var tasks = new List<EvaluationTask>();
        foreach (var record in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(record))
            {
                continue;
            }

            var task = JsonSerializer.Deserialize<EvaluationTask>(record, _jsonOptions)
                ?? throw new InvalidDataException(EvaluationErrors.EmptyTaskRecord(path));
            tasks.Add(task with { Fingerprint = FingerprintOf(record) });
        }

        return tasks;
    }

    private static string FingerprintOf(string record) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(record)));
}
