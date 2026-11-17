using System.Text.Json.Nodes;
using ReviewAgent.Infrastructure.Agents.LanguageModel.ProviderExtensions;

namespace ReviewAgent.Infrastructure.Tests.Agents.LanguageModel.ProviderExtensions;

public sealed class ToolCallExtrasTests
{
    // A Gemini 3 response: the tool call carries a thought signature in extra_content.
    private const string ResponseWithSignature =
        """
        {
          "choices": [{
            "message": {
              "role": "assistant",
              "tool_calls": [{
                "id": "call-1",
                "type": "function",
                "function": { "name": "get_pull_request", "arguments": "{}" },
                "extra_content": { "google": { "thought_signature": "signature-1" } }
              }]
            }
          }]
        }
        """;

    // Another provider's response: no extra_content.
    private const string ResponseWithoutSignature =
        """
        {
          "choices": [{
            "message": {
              "role": "assistant",
              "tool_calls": [{
                "id": "call-1",
                "type": "function",
                "function": { "name": "get_pull_request", "arguments": "{}" }
              }]
            }
          }]
        }
        """;

    // The next request as the OpenAI SDK writes it: the same tool call, without extra_content.
    private const string RequestWithoutSignature =
        """
        {
          "model": "gemini",
          "messages": [
            { "role": "user", "content": "Review the pull request." },
            {
              "role": "assistant",
              "tool_calls": [{
                "id": "call-1",
                "type": "function",
                "function": { "name": "get_pull_request", "arguments": "{}" }
              }]
            },
            { "role": "tool", "tool_call_id": "call-1", "content": "Title: Add order discounts" }
          ]
        }
        """;

    [Fact]
    public void Remembered_signature_is_added_back_to_the_same_tool_call()
    {
        var extras = new ToolCallExtras();
        extras.RememberFrom(BinaryData.FromString(ResponseWithSignature));

        var updated = extras.AddTo(BinaryData.FromString(RequestWithoutSignature));

        Assert.NotNull(updated);
        var toolCall = JsonNode.Parse(updated.ToString())!["messages"]![1]!["tool_calls"]![0]!;
        Assert.Equal("signature-1", (string?)toolCall["extra_content"]!["google"]!["thought_signature"]);
    }

    [Fact]
    public void Request_is_unchanged_when_nothing_was_remembered()
    {
        var extras = new ToolCallExtras();

        Assert.Null(extras.AddTo(BinaryData.FromString(RequestWithoutSignature)));
    }

    [Fact]
    public void Response_without_extra_content_adds_nothing()
    {
        var extras = new ToolCallExtras();
        extras.RememberFrom(BinaryData.FromString(ResponseWithoutSignature));

        Assert.Null(extras.AddTo(BinaryData.FromString(RequestWithoutSignature)));
    }

    [Fact]
    public void Body_that_is_not_json_is_ignored()
    {
        var extras = new ToolCallExtras();
        extras.RememberFrom(BinaryData.FromString(ResponseWithSignature));

        extras.RememberFrom(BinaryData.FromString("not json"));

        Assert.Null(extras.AddTo(BinaryData.FromString(string.Empty)));
    }
}
