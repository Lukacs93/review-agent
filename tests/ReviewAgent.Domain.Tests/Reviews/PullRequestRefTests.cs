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
    [InlineData("Lukacs93", "review-agent-demo")]
    [InlineData("a", "my.repo_1")]
    public void GitHub_style_names_are_accepted(string repositoryOwner, string repositoryName)
    {
        var pullRequest = new PullRequestRef(repositoryOwner, repositoryName, PullRequestNumber);

        Assert.Equal(repositoryOwner, pullRequest.Owner);
        Assert.Equal(repositoryName, pullRequest.Repository);
    }

    [Fact]
    public void Too_long_names_are_rejected()
    {
        var ownerError = Assert.Throws<DomainException>(
            () => new PullRequestRef(new string('a', 40), RepositoryName, PullRequestNumber));
        var repositoryError = Assert.Throws<DomainException>(
            () => new PullRequestRef(RepositoryOwner, new string('a', 101), PullRequestNumber));

        Assert.Equal(ValueObjectErrors.RepositoryOwnerInvalid, ownerError.Message);
        Assert.Equal(ValueObjectErrors.RepositoryNameInvalid, repositoryError.Message);
    }

    [Theory]
    [InlineData("", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerRequired)]
    [InlineData(RepositoryOwner, " ", PullRequestNumber, ValueObjectErrors.RepositoryNameRequired)]
    [InlineData(RepositoryOwner, RepositoryName, 0, ValueObjectErrors.PullRequestNumberNotPositive)]
    [InlineData(RepositoryOwner, RepositoryName, -3, ValueObjectErrors.PullRequestNumberNotPositive)]
    [InlineData("-test", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData("test-", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData("te--st", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData("te_st", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData("..", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData("a/b", RepositoryName, PullRequestNumber, ValueObjectErrors.RepositoryOwnerInvalid)]
    [InlineData(RepositoryOwner, "..", PullRequestNumber, ValueObjectErrors.RepositoryNameInvalid)]
    [InlineData(RepositoryOwner, ".", PullRequestNumber, ValueObjectErrors.RepositoryNameInvalid)]
    [InlineData(RepositoryOwner, "a/b", PullRequestNumber, ValueObjectErrors.RepositoryNameInvalid)]
    [InlineData(RepositoryOwner, "my repo", PullRequestNumber, ValueObjectErrors.RepositoryNameInvalid)]
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
