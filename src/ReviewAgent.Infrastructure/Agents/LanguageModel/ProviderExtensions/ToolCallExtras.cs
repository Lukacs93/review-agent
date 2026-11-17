using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel.ProviderExtensions;

/// <summary>
/// Keeps the provider-specific data a model attaches to its tool calls, and puts it back when the same tool calls
/// are sent to the model again. Gemini 3 attaches a thought signature to each tool call
/// (<c>extra_content.google.thought_signature</c>) and rejects the next request without it, but the OpenAI SDK does
/// not know the field and drops it. Providers that send no <c>extra_content</c> are not affected.
/// </summary>
internal sealed class ToolCallExtras
{
    private const string ExtraContent = "extra_content";

    private readonly ConcurrentDictionary<string, JsonNode> _byCallId = new(StringComparer.Ordinal);

    /// <summary>Remembers the extra data of the tool calls in a chat completion response.</summary>
    public void RememberFrom(BinaryData response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (Parse(response) is not JsonObject root || root["choices"] is not JsonArray choices)
        {
            return;
        }

        var toolCalls = choices
            .OfType<JsonObject>()
            .Select(choice => choice["message"]?["tool_calls"])
            .OfType<JsonArray>()
            .SelectMany(calls => calls)
            .OfType<JsonObject>();

        foreach (var toolCall in toolCalls)
        {
            if (CallId(toolCall) is { } id && toolCall[ExtraContent] is { } extra)
            {
                // A JSON node can belong to only one document. Keep a copy of its own, detached from the response.
                _byCallId[id] = extra.DeepClone();
            }
        }
    }

    /// <summary>
    /// Adds the remembered extra data to the matching tool calls of a chat completion request; returns the new
    /// request body, or <see langword="null"/> when nothing had to be added.
    /// </summary>
    public BinaryData? AddTo(BinaryData request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_byCallId.IsEmpty || Parse(request) is not JsonObject root || root["messages"] is not JsonArray messages)
        {
            return null;
        }

        var toolCalls = messages
            .OfType<JsonObject>()
            .Select(message => message["tool_calls"])
            .OfType<JsonArray>()
            .SelectMany(calls => calls)
            .OfType<JsonObject>();

        var changed = false;
        foreach (var toolCall in toolCalls)
        {
            if (toolCall[ExtraContent] is null
                && CallId(toolCall) is { } id
                && _byCallId.TryGetValue(id, out var extra))
            {
                // The same tool call is sent again in every later request, and a JSON node can belong to only one
                // document, so each request gets a new copy.
                toolCall[ExtraContent] = extra.DeepClone();
                changed = true;
            }
        }

        return changed ? BinaryData.FromString(root.ToJsonString()) : null;
    }

    private static string? CallId(JsonObject toolCall) =>
        toolCall["id"] is JsonValue id && id.TryGetValue<string>(out var value) ? value : null;

    private static JsonNode? Parse(BinaryData json)
    {
        try
        {
            return JsonNode.Parse(json.ToString());
        }
        catch (JsonException)
        {
            // Not JSON, for example an empty body: nothing to remember or add.
            return null;
        }
    }
}
