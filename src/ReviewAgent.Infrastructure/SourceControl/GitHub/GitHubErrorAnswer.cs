using System.Text.Json;

namespace ReviewAgent.Infrastructure.SourceControl.GitHub;

/// <summary>
/// Reads GitHub's error answer: <c>{"message": "...", "errors": [...]}</c>, where an item of <c>errors</c> is a text
/// or an object that may have a <c>message</c>.
/// </summary>
internal static class GitHubErrorAnswer
{
    /// <summary>
    /// GitHub's message, followed by the messages of its error list; <see langword="null"/> when the answer is not
    /// GitHub's JSON error, for example an HTML page from a proxy.
    /// </summary>
    public static string? MessageOf(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        try
        {
            using var document = JsonDocument.Parse(answer);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("message", out var messageElement)
                || messageElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var message = messageElement.GetString();
            var hasErrorList = root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array;
            List<string> details = hasErrorList
                ? errors.EnumerateArray().Select(DetailOf).OfType<string>().ToList()
                : [];

            return details.Count == 0 ? message : $"{message} ({string.Join("; ", details)})";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? DetailOf(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
        {
            return error.GetString();
        }

        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            return message.GetString();
        }

        return null;
    }
}
