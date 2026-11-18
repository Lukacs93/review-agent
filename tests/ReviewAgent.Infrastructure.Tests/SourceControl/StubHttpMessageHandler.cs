using System.Net;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

/// <summary>
/// Answers HTTP requests with prepared responses instead of calling GitHub, and records the requests.
/// Responses prepared for the same address are given in order, the last one repeated; anything else is 404.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<PreparedResponse>> _responses = new(StringComparer.Ordinal);

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>The bodies of the requests, in order; read when the request is sent, empty when it has none.</summary>
    public List<string> RequestBodies { get; } = [];

    /// <summary>
    /// Prepares a response for a path and query, for example <c>/repos/test/demo/pulls/1</c>. With
    /// <paramref name="nextPage"/>, the response's <c>Link</c> header points to that address as the next page.
    /// </summary>
    public void Respond(string pathAndQuery, HttpStatusCode status, string content = "", Uri? nextPage = null)
    {
        if (!_responses.TryGetValue(pathAndQuery, out var queue))
        {
            queue = new Queue<PreparedResponse>();
            _responses[pathAndQuery] = queue;
        }

        queue.Enqueue(new PreparedResponse(status, content, nextPage));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (!_responses.TryGetValue(request.RequestUri!.PathAndQuery, out var queue))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
        }

        var prepared = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        var response = new HttpResponseMessage(prepared.Status) { Content = new StringContent(prepared.Content) };
        if (prepared.NextPage is not null)
        {
            response.Headers.Add("Link", $"<{prepared.NextPage}>; rel=\"next\"");
        }

        return response;
    }

    private sealed record PreparedResponse(HttpStatusCode Status, string Content, Uri? NextPage);
}
