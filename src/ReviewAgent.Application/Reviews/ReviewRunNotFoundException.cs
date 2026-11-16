namespace ReviewAgent.Application.Reviews;

/// <summary>Thrown when a command refers to a review run that does not exist.</summary>
public sealed class ReviewRunNotFoundException : Exception
{
    public ReviewRunNotFoundException()
    {
    }

    public ReviewRunNotFoundException(string message)
        : base(message)
    {
    }

    public ReviewRunNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
