using System.Net;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

/// <summary>
/// Answers HTTP requests with prepared responses instead of calling GitHub, and records the requests.
/// Responses prepared for the same address are given in order, the last one repeated; anything else is 404.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<(HttpStatusCode Status, string Content)>> _responses =
        new(StringComparer.Ordinal);

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Prepares a response for a path and query, for example <c>/repos/test/demo/pulls/1</c>.</summary>
    public void Respond(string pathAndQuery, HttpStatusCode status, string content = "")
    {
        if (!_responses.TryGetValue(pathAndQuery, out var queue))
        {
            queue = new Queue<(HttpStatusCode, string)>();
            _responses[pathAndQuery] = queue;
        }

        queue.Enqueue((status, content));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var status = HttpStatusCode.NotFound;
        var content = string.Empty;
        if (_responses.TryGetValue(request.RequestUri!.PathAndQuery, out var queue))
        {
            (status, content) = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }

        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(content) });
    }
}
