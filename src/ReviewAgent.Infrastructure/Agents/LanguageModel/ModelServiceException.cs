namespace ReviewAgent.Infrastructure.Agents.LanguageModel;

/// <summary>
/// A model call failed: the model's API answered with an error or could not be reached, after the retries. The
/// original error is the inner exception.
/// </summary>
public sealed class ModelServiceException : Exception
{
    public ModelServiceException()
    {
    }

    public ModelServiceException(string message)
        : base(message)
    {
    }

    public ModelServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
