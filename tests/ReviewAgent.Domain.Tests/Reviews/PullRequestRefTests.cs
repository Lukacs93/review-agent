using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Domain.Tests.Reviews;

public sealed class PullRequestRefTests
{
    private const string RepositoryOwner = "test";
    private const string RepositoryName = "demo";
    private const int PullRequestNumber = 1;

    [Fact]
    public void ToString_uses_owner_repository_and_number()
    {
        var pullRequest = new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber);

        Assert.Equal("test/demo#1", pullRequest.ToString());
    }

    [Fact]
    public void Owner_and_repository_are_trimmed()
    {
        var pullRequest = new PullRequestRef($" {RepositoryOwner} ", $" {RepositoryName} ", PullRequestNumber);

        Assert.Equal(RepositoryOwner, pullRequest.Owner);
        Assert.Equal(RepositoryName, pullRequest.Repository);
    }

    [Fact]
    public void References_to_the_same_pull_request_are_equal()
    {
        Assert.Equal(
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber),
            new PullRequestRef(RepositoryOwner, RepositoryName, PullRequestNumber));
    }

    [Theory]
    [InlineData("", RepositoryName, PullRequestNumber)]
    [InlineData(RepositoryOwner, " ", PullRequestNumber)]
    [InlineData(RepositoryOwner, RepositoryName, 0)]
    [InlineData(RepositoryOwner, RepositoryName, -3)]
    public void Invalid_reference_is_rejected(string repositoryOwner, string repositoryName, int pullRequestNumber)
    {
        Assert.Throws<DomainException>(() => new PullRequestRef(repositoryOwner, repositoryName, pullRequestNumber));
    }
}
