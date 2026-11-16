namespace ReviewAgent.Domain.Reviews.Errors;

/// <summary>The messages of the <see cref="DomainException"/>s thrown by the review value objects.</summary>
public static class ValueObjectErrors
{
    // PullRequestRef
    public const string RepositoryOwnerRequired = "Repository owner is required.";
    public const string RepositoryNameRequired = "Repository name is required.";
    public const string PullRequestNumberNotPositive = "Pull request number must be positive.";

    public const string RepositoryOwnerInvalid =
        "Repository owner may contain only letters, digits and single hyphens, cannot start or end with a hyphen, "
        + "and is at most 39 characters long.";

    public const string RepositoryNameInvalid =
        "Repository name may contain only letters, digits, '.', '-' and '_', is at most 100 characters long, "
        + "and cannot be '.' or '..'.";

    // CodeLocation
    public const string FilePathRequired = "File path is required.";
    public const string FilePathNotRelative = "File path must be relative to the repository root.";
    public const string LineNotPositive = "Line number must be positive.";
}
