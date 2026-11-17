using System.ClientModel;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// Makes a failure reason useful. When the model's API rejects a request, the exception only says the status
/// ("400 Bad Request"); the reason is in the response body, so it is added here.
/// </summary>
internal static class ModelErrorDetails
{
    private const int MaxBodyLength = 1000;

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not ClientResultException clientError
            || clientError.GetRawResponse()?.Content?.ToString() is not { Length: > 0 } body)
        {
            return exception.Message;
        }

        // Marks that the text was cut off.
        const string CutOffMark = "…";

        var isTooLong = body.Length > MaxBodyLength;
        var trimmed = isTooLong
            ? body[..MaxBodyLength] + CutOffMark
            : body;
        return $"{exception.Message} {trimmed}";
    }
}
