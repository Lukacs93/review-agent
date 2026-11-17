using System.ClientModel;
using System.ClientModel.Primitives;

namespace ReviewAgent.Infrastructure.Agents.LanguageModel.ProviderExtensions;

/// <summary>
/// A step in the OpenAI SDK's HTTP pipeline that applies <see cref="ToolCallExtras"/>: it remembers the extra data
/// of the tool calls in each response and adds it back to the next requests. One instance per chat client, so the
/// data of one review never reaches another.
/// </summary>
internal sealed class ToolCallExtrasPolicy : PipelinePolicy
{
    private readonly ToolCallExtras _extras = new();

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(message);

        AddExtras(message);
        ProcessNext(message, pipeline, currentIndex);
        RememberExtras(message);
    }

    public override async ValueTask ProcessAsync(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(message);

        AddExtras(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
        RememberExtras(message);
    }

    private void AddExtras(PipelineMessage message)
    {
        var content = message.Request.Content;
        if (content is null)
        {
            return;
        }

        // The OpenAI SDK does not keep the request body as text but as a BinaryContent, which can only write
        // itself to a stream. It is written to memory, so the JSON can be read and changed.
        using var body = new MemoryStream();
        content.WriteTo(body);

        var updatedBody = _extras.AddTo(BinaryData.FromBytes(body.ToArray()));
        if (updatedBody is not null)
        {
            message.Request.Content = BinaryContent.Create(updatedBody);
        }
    }

    private void RememberExtras(PipelineMessage message)
    {
        if (message.Response is { IsError: false } response)
        {
            _extras.RememberFrom(response.Content);
        }
    }
}
