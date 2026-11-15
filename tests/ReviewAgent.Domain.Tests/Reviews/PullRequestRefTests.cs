using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

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
    [InlineData("", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerRequired)]
    [InlineData(RepositoryOwner, " ", PullRequestNumber, ValueObjectErrors.RepositoryNameRequired)]
    [InlineData(RepositoryOwner, RepositoryName, 0, ValueObjectErrors.PullRequestNumberNotPositive)]
    [InlineData(RepositoryOwner, RepositoryName, -3, ValueObjectErrors.PullRequestNumberNotPositive)]
    public void Invalid_reference_is_rejected(
        string repositoryOwner,
        string repositoryName,
        int pullRequestNumber,
        string expectedMessage)
    {
        var error = Assert.Throws<DomainException>(
            () => new PullRequestRef(repositoryOwner, repositoryName, pullRequestNumber));

        Assert.Equal(expectedMessage, error.Message);
    }
}
