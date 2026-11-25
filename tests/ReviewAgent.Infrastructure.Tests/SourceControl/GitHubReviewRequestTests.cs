using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.SourceControl.GitHub;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class GitHubReviewRequestTests
{
    private const string Reason = "Entity properties must not have public setters.";

    [Fact]
    public void Replacement_code_is_published_as_a_suggested_change()
    {
        var body = CommentBodyOf(replacementCode: "    public decimal DiscountAmount { get; private set; }");

        Assert.Equal(
            $"**DDD-01**: {Reason}\n\n```suggestion\n    public decimal DiscountAmount {{ get; private set; }}\n```",
            body);
    }

    // Three backticks in the code would end a block fenced with three.
    [Fact]
    public void Code_with_backticks_gets_a_longer_fence()
    {
        var body = CommentBodyOf(replacementCode: "    var fence = \"```\";");

        Assert.Equal($"**DDD-01**: {Reason}\n\n````suggestion\n    var fence = \"```\";\n````", body);
    }

    // Comments proposed before the replacement code may hold prose: it is not offered as a change.
    [Fact]
    public void Free_text_fix_is_published_as_text()
    {
        var body = CommentBodyOf(replacementCode: null, suggestedFix: "Make the setter private.");

        Assert.Equal($"**DDD-01**: {Reason}\n\nSuggested fix: Make the setter private.", body);
    }

    private static string CommentBodyOf(string? replacementCode, string? suggestedFix = null)
    {
        var publication = new ReviewPublication(
            Guid.NewGuid(),
            new PullRequestRef("test", "demo", 1),
            ReviewedCommitSha: null,
            "One domain issue.",
            [new CommentToPublish("src/Order.cs", 13, "DDD-01", Reason, suggestedFix, replacementCode)]);

        return Assert.Single(GitHubReviewRequest.From(publication).Comments).Body;
    }
}
