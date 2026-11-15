using System.Globalization;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Domain.Reviews;

/// <summary>Points to a pull request in a hosted repository, for example <c>test/demo#1</c>.</summary>
public sealed record PullRequestRef
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

        Owner = owner.Trim();
        Repository = repository.Trim();
        Number = number;
    }

    public string Owner { get; }

    public string Repository { get; }

    public int Number { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Owner}/{Repository}#{Number}");
}
