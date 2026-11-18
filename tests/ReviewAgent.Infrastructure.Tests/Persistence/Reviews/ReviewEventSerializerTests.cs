using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Events;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Persistence.Reviews;
using ReviewAgent.Infrastructure.Persistence.Reviews.Errors;

namespace ReviewAgent.Infrastructure.Tests.Persistence.Reviews;

public sealed class ReviewEventSerializerTests
{
    private const string UnknownEventType = "ReviewArchived";

    private static ReviewRunId RunId => new(Guid.Parse("6f1c2a9e-3b4d-4e5f-8a7b-1c2d3e4f5a6b"));

    private static CommentId CommentId => new(Guid.Parse("0a1b2c3d-4e5f-4a6b-9c8d-7e6f5a4b3c2d"));

    private static DateTimeOffset Now => new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Samples_cover_every_review_event()
    {
        var domainEventTypes = typeof(ReviewEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ReviewEvent)) && !type.IsAbstract)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        var sampleTypes = SampleEvents()
            .Select(reviewEvent => reviewEvent.GetType().Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(domainEventTypes, sampleTypes);
    }

    [Fact]
    public void Every_review_event_survives_a_round_trip()
    {
        Assert.All(SampleEvents(), reviewEvent =>
        {
            var eventType = ReviewEventSerializer.TypeNameOf(reviewEvent);
            var data = ReviewEventSerializer.Serialize(reviewEvent);

            Assert.Equal(reviewEvent, ReviewEventSerializer.Deserialize(eventType, data));
        });
    }

    [Fact]
    public void Severity_is_stored_as_text()
    {
        var proposed = SampleEvents().OfType<CommentProposed>().Single();

        var data = ReviewEventSerializer.Serialize(proposed);

        Assert.Contains("\"Severity\":\"Error\"", data, StringComparison.Ordinal);
    }

    [Fact]
    public void Finished_event_stored_without_the_reviewed_commit_is_read()
    {
        var finished = new ReviewFinished(RunId, Now, "One architecture issue.", ReviewedCommitSha: null);
        var data = ReviewEventSerializer.Serialize(finished)
            .Replace(",\"ReviewedCommitSha\":null", string.Empty, StringComparison.Ordinal);

        var read = ReviewEventSerializer.Deserialize(ReviewEventSerializer.TypeNameOf(finished), data);

        Assert.DoesNotContain("ReviewedCommitSha", data, StringComparison.Ordinal);
        Assert.Equal(finished, read);
    }

    [Fact]
    public void Unknown_event_type_is_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ReviewEventSerializer.Deserialize(UnknownEventType, "{}"));

        Assert.Equal(ReviewEventStoreErrors.UnknownEventType(UnknownEventType), error.Message);
    }

    private static ReviewEvent[] SampleEvents() =>
    [
        new ReviewStarted(RunId, Now, new PullRequestRef("test", "demo", 1)),
        new ToolCalled(RunId, Now, "get_diff", "{\"path\":\"src/Order.cs\"}"),
        new CommentProposed(
            RunId,
            Now,
            CommentId,
            new CodeLocation("src/Orders.Api/Controllers/OrdersController.cs", 12),
            new RuleId("ARCH-01"),
            Severity.Error,
            "Use an application handler instead of the DbContext.",
            null),
        new CommentRejected(RunId, Now, CommentId, "The query does not change state."),
        new VerificationFinished(RunId, Now, "Kept 1 comment and rejected 0."),
        new ReviewFinished(RunId, Now, "One architecture issue.", "9fceb02d0ae598e95dc970b74767f19372d61af8"),
        new ReviewFailed(RunId, Now, "The model did not answer."),
        new CommentApproved(RunId, Now, CommentId),
        new CommentEdited(RunId, Now, CommentId, "Move this query to a handler."),
        new CommentDiscarded(RunId, Now, CommentId),
        new PublishingRequested(RunId, Now),
        new PublishingFailed(RunId, Now, "GitHub answered 422."),
        new CommentPublished(RunId, Now, CommentId),
        new ReviewPublished(RunId, Now),
    ];
}
