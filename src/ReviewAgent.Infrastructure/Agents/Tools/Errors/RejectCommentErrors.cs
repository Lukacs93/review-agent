namespace ReviewAgent.Infrastructure.Agents.Tools.Errors;

/// <summary>
/// What <c>reject_comment</c> answers when it does not reject a comment. The model reads these, so each one says
/// what to correct.
/// </summary>
internal static class RejectCommentErrors
{
    private const string Prefix = "Not rejected: ";

    public const string ReasonRequired = Prefix + "give the reason why the comment is wrong.";

    public static string UnknownComment(string commentId) =>
        $"{Prefix}'{commentId}' is not one of the proposed comments. Use an ID from the list in the task.";

    public static string AlreadyRejected(string commentId) =>
        $"{Prefix}the comment '{commentId}' was already rejected.";
}
