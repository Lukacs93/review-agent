using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace ReviewAgent.Integration.Tests;

/// <summary>Checks that an error answer is a problem details document (RFC 9457) with the expected content.</summary>
internal static class ProblemDetailsAssert
{
    private const string ProblemContentType = "application/problem+json";

    public static async Task HasProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedDetail)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(expectedDetail, problem.Detail);
    }
}
