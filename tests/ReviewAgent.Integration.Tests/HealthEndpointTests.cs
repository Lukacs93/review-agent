using System.Net;

namespace ReviewAgent.Integration.Tests;

public sealed class HealthEndpointTests(ReviewAgentApiFactory factory) : IClassFixture<ReviewAgentApiFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
