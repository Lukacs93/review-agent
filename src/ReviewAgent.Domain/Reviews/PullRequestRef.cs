using System.Globalization;
using System.Text.RegularExpressions;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Domain.Reviews;

/// <summary>
/// Points to a pull request in a hosted repository, for example <c>test/demo#1</c>. The owner and repository
/// names follow GitHub's rules, so they are safe to put into URLs and file paths.
/// </summary>
public sealed partial record PullRequestRef
{
    public PullRequestRef(string owner, string repository, int number)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new DomainException(ValueObjectErrors.RepositoryOwnerRequired);
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            throw new DomainException(ValueObjectErrors.RepositoryNameRequired);
        }

        if (number <= 0)
        {
            throw new DomainException(ValueObjectErrors.PullRequestNumberNotPositive);
        }

        var trimmedOwner = owner.Trim();
        if (!OwnerPattern().IsMatch(trimmedOwner))
        {
            throw new DomainException(ValueObjectErrors.RepositoryOwnerInvalid);
        }

        var trimmedRepository = repository.Trim();
        if (!RepositoryPattern().IsMatch(trimmedRepository) || trimmedRepository is "." or "..")
        {
            throw new DomainException(ValueObjectErrors.RepositoryNameInvalid);
        }

        Owner = trimmedOwner;
        Repository = trimmedRepository;
        Number = number;
    }

    public string Owner { get; }

    public string Repository { get; }

    public int Number { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Owner}/{Repository}#{Number}");

    // GitHub user and organization names: letters, digits and single hyphens, no hyphen at either end, at most 39.
    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    // GitHub repository names: letters, digits, '.', '-' and '_', at most 100.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryPattern();
}
