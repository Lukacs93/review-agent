using System.Globalization;

namespace ReviewAgent.Domain.Reviews;

/// <summary>Points to a pull request in a hosted repository, for example <c>octo/demo#1</c>.</summary>
public sealed record PullRequestRef
{
    public PullRequestRef(string owner, string repository, int number)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new DomainException("Repository owner is required.");
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            throw new DomainException("Repository name is required.");
        }

        if (number <= 0)
        {
            throw new DomainException("Pull request number must be positive.");
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
