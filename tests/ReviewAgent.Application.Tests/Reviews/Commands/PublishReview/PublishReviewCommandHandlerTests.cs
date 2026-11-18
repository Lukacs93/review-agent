using ReviewAgent.Application.Reviews.Commands.PublishReview;
using ReviewAgent.Application.Reviews.Commands.RequestPublishing;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Tests.Reviews.Commands.PublishReview;

public sealed class PublishReviewCommandHandlerTests
{
    private const string ConnectionLost = "The connection to GitHub was lost.";

    private readonly InMemoryReviewRunRepository _repository = new();
    private readonly InMemoryReviewPublisher _publisher = new();
    private readonly FixedTimeProvider _timeProvider = new(TestReviewRuns.Now);

    private PublishReviewCommandHandler Handler => new(_repository, _publisher, _timeProvider);

    [Fact]
    public async Task Approved_comments_are_published_and_recorded()
    {
        var runId = await TestReviewRuns.SavePublishingRunAsync(_repository);

        await Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None);

        var publication = Assert.Single(_publisher.Published);
        Assert.Equal(runId, publication.RunId);
        Assert.Equal(TestReviewRuns.Summary, publication.Summary);
        Assert.Equal(TestReviewRuns.CommitSha, publication.ReviewedCommitSha);
        var comment = Assert.Single(publication.Comments);
        Assert.Equal(TestReviewRuns.FilePath, comment.FilePath);
        Assert.Equal(TestReviewRuns.Line, comment.Line);
        Assert.Equal(TestReviewRuns.Rule, comment.Rule);
        Assert.Equal(TestReviewRuns.CommentBody, comment.Body);

        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.Published, run.Status);
        Assert.True(Assert.Single(run.Comments).IsPublished);
    }

    // The application stopped after the review went out, but before it was recorded: the run is still Publishing,
    // and at the next start it is published again.
    [Fact]
    public async Task Review_already_on_the_pull_request_is_recorded_without_publishing_it_again()
    {
        var runId = await TestReviewRuns.SavePublishingRunAsync(_repository);
        _publisher.Published.Add(ReviewPublication.From(await LoadAsync(runId)));

        await Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None);

        Assert.Single(_publisher.Published);
        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.Published, run.Status);
        Assert.True(Assert.Single(run.Comments).IsPublished);
    }

    // The review went out, but the answer was lost, so the publishing failed; asked again, it is not repeated.
    [Fact]
    public async Task Review_whose_answer_was_lost_is_not_published_twice()
    {
        var runId = await TestReviewRuns.SavePublishingRunAsync(_repository);
        _publisher.FailAfterPublishing = new HttpRequestException(ConnectionLost);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None));

        await new RequestPublishingCommandHandler(_repository, new RecordingPublishingScheduler(), _timeProvider)
            .HandleAsync(new RequestPublishingCommand(runId), CancellationToken.None);
        await Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None);

        Assert.Single(_publisher.Published);
        Assert.Equal(ReviewStatus.Published, (await LoadAsync(runId)).Status);
    }

    [Fact]
    public async Task Failed_publishing_waits_for_the_developer_with_the_reason()
    {
        var runId = await TestReviewRuns.SavePublishingRunAsync(_repository);
        _publisher.FailBeforePublishing = new HttpRequestException(ConnectionLost);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None));

        Assert.Empty(_publisher.Published);
        var run = await LoadAsync(runId);
        Assert.Equal(ReviewStatus.AwaitingApproval, run.Status);
        Assert.Equal(PublishingErrors.Failed(ConnectionLost), run.PublishingFailureReason);
        Assert.False(Assert.Single(run.Comments).IsPublished);
    }

    [Fact]
    public async Task Run_not_being_published_is_left_alone()
    {
        var runId = await TestReviewRuns.SaveApprovedRunAsync(_repository);

        await Handler.HandleAsync(new PublishReviewCommand(runId), CancellationToken.None);

        Assert.Empty(_publisher.Published);
        Assert.Equal(ReviewStatus.AwaitingApproval, (await LoadAsync(runId)).Status);
    }

    private async Task<ReviewRun> LoadAsync(Guid runId)
    {
        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        return run;
    }
}
