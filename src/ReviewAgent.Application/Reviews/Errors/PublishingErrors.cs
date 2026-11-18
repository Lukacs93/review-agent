namespace ReviewAgent.Application.Reviews.Errors;

/// <summary>Why publishing failed, shown to the developer, who can ask for publishing again.</summary>
public static class PublishingErrors
{
    public static string Failed(string reason) => $"Publishing failed; ask for it again. Reason: {reason}";
}
