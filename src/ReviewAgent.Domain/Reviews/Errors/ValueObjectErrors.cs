namespace ReviewAgent.Domain.Reviews.Errors;

/// <summary>The messages of the <see cref="DomainException"/>s thrown by the review value objects.</summary>
public static class ValueObjectErrors
{
    // PullRequestRef
    public const string RepositoryOwnerRequired = "Repository owner is required.";
    public const string RepositoryNameRequired = "Repository name is required.";
    public const string PullRequestNumberNotPositive = "Pull request number must be positive.";

    // CodeLocation
    public const string FilePathRequired = "File path is required.";
    public const string FilePathNotRelative = "File path must be relative to the repository root.";
    public const string LineNotPositive = "Line number must be positive.";
}
