namespace ReviewAgent.Application.SourceControl;

/// <summary>Thrown when the referenced pull request does not exist in the source.</summary>
public sealed class PullRequestNotFoundException : Exception
{
    public PullRequestNotFoundException()
    {
    }

    public PullRequestNotFoundException(string message)
        : base(message)
    {
    }

    public PullRequestNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
