using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.GitHub.Errors;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class GitHubPaginationTests
{
    private const string NextPage = "https://api.github.com/pulls?page=2";

    private static readonly Uri _apiAddress = new("https://api.github.com/");

    [Theory]
    [InlineData("<https://api.github.com/pulls?page=2>; rel=\"next\", "
        + "<https://api.github.com/pulls?page=5>; rel=\"last\"")]
    [InlineData("<https://api.github.com/pulls?page=5>; rel=\"last\", "
        + "<https://api.github.com/pulls?page=2>; rel=\"next\"")]
    [InlineData("<https://api.github.com/pulls?page=2>; rel=next")]
    [InlineData("<https://api.github.com/pulls?page=2>; REL=\"Next\"")]
    [InlineData("<https://api.github.com/pulls?page=2>; rel=\"prefetch next\"")]
    [InlineData("<https://api.github.com/pulls?page=2> ; title=\"Page; next, of files\" ; rel=\"next\"")]
    public void Next_page_is_read_in_every_form_the_standard_allows(string linkHeader)
    {
        using var response = ResponseWithLink(linkHeader);

        Assert.Equal(new Uri(NextPage), GitHubPagination.NextPageUri(response, _apiAddress));
    }

    [Fact]
    public void Comma_inside_the_address_does_not_split_the_link()
    {
        const string pageWithComma = "https://api.github.com/search/code?q=a,b&page=2";
        using var response = ResponseWithLink($"<{pageWithComma}>; rel=\"next\"");

        Assert.Equal(new Uri(pageWithComma), GitHubPagination.NextPageUri(response, _apiAddress));
    }

    [Theory]
    [InlineData("<https://api.github.com/pulls?page=1>; rel=\"prev\", "
        + "<https://api.github.com/pulls?page=1>; rel=\"first\"")]
    [InlineData("<https://api.github.com/pulls?page=2>; title=\"next\"")]
    public void Response_without_a_next_link_is_the_last_page(string linkHeader)
    {
        using var response = ResponseWithLink(linkHeader);

        Assert.Null(GitHubPagination.NextPageUri(response, _apiAddress));
    }

    [Fact]
    public void Response_without_a_link_header_is_the_last_page()
    {
        using var response = new HttpResponseMessage();

        Assert.Null(GitHubPagination.NextPageUri(response, _apiAddress));
    }

    [Fact]
    public void Next_page_outside_the_api_address_is_rejected()
    {
        var elsewhere = new Uri("https://example.com/pulls?page=2");
        using var response = ResponseWithLink($"<{elsewhere}>; rel=\"next\"");

        var error = Assert.Throws<InvalidOperationException>(
            () => GitHubPagination.NextPageUri(response, _apiAddress));

        Assert.Equal(GitHubErrors.NextPageOutsideApi(elsewhere), error.Message);
    }

    private static HttpResponseMessage ResponseWithLink(string linkHeader)
    {
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Link", linkHeader);
        return response;
    }
}
