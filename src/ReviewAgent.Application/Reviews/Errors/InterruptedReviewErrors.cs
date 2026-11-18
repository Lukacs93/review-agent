namespace ReviewAgent.Application.Reviews.Errors;

/// <summary>The failure reason of a review that was interrupted, shown to the developer.</summary>
public static class InterruptedReviewErrors
{
    public const string Interrupted =
        "The review was interrupted because the application stopped. Start the review again.";
}
