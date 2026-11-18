using System.ClientModel;
using System.Text.Json;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Makes a failure reason useful. When the model's API rejects a request, the exception only says the status
/// ("HTTP 400"); the API's own error message is in its answer, so it is added here.
/// </summary>
internal static class ModelErrorDetails
{
    /// <summary>The exception's message, followed by the API's error message when its answer has one.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var answer = AnswerOf(exception);
        var message = answer is null ? null : MessageOf(answer);
        return message is null ? exception.Message : $"{exception.Message} {message}";
    }

    /// <summary>The whole answer of the model's API, for the log; <see langword="null"/> when there is none.</summary>
    public static string? AnswerOf(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not ClientResultException clientError)
        {
            return null;
        }

        var answer = clientError.GetRawResponse()?.Content?.ToString();
        return string.IsNullOrEmpty(answer) ? null : answer;
    }

    /// <summary>
    /// The <c>error.message</c> of an OpenAI-compatible error answer; an answer that is a list is read from its first
    /// item. <see langword="null"/> when the answer is not such JSON, for example an HTML page from a proxy.
    /// </summary>
    internal static string? MessageOf(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        try
        {
            using var document = JsonDocument.Parse(answer);
            var root = document.RootElement;
            var isList = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0;
            var errorAnswer = isList ? root[0] : root;

            if (errorAnswer.ValueKind == JsonValueKind.Object
                && errorAnswer.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
