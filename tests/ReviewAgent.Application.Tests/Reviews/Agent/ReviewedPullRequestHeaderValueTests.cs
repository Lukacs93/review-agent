using ReviewAgent.Application.Reviews.Agent;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Agent;

public sealed class ReviewedPullRequestHeaderValueTests
{
    private const string RepositoryOwner = "test";
    private const string RepositoryName = "demo";
    private const int PullRequestNumber = 1;

    private static PullRequestRef PullRequest => new(RepositoryOwner, RepositoryName, PullRequestNumber);

    [Fact]
    public void Value_is_owner_repository_and_number()
    {
        Assert.Equal("test/demo/1", ReviewedPullRequestHeaderValue.Format(PullRequest));
    }

    [Fact]
    public void Formatted_value_is_read_back()
    {
        var value = ReviewedPullRequestHeaderValue.Format(PullRequest);

        Assert.Equal(PullRequest, ReviewedPullRequestHeaderValue.ParseOrDefault(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("test/demo")]
    [InlineData("test/demo/1/extra")]
    [InlineData("test/demo/one")]
    [InlineData("test/demo/-1")]
    [InlineData("test/demo/0")]
    [InlineData("../demo/1")]
    public void Invalid_value_is_not_read(string? value)
    {
        Assert.Null(ReviewedPullRequestHeaderValue.ParseOrDefault(value));
    }
}
