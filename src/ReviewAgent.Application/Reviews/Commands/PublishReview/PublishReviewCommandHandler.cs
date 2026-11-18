using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Commands.PublishReview;

/// <summary>
/// Publishes the review once, then records it. If the pull request already has this run's review, because an
/// earlier attempt published it but stopped before recording it, only the recording is done. If publishing fails,
/// the run waits for the developer again, with the reason, and the exception is passed on to be logged.
/// </summary>
public sealed class PublishReviewCommandHandler(
    IReviewRunRepository repository,
    IReviewPublisher publisher,
    TimeProvider timeProvider) : ICommandHandler<PublishReviewCommand>
{
    public async Task HandleAsync(PublishReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await repository.GetAsync(new ReviewRunId(command.RunId), cancellationToken);
        if (run.Status != ReviewStatus.Publishing)
        {
            // Nothing to publish: already published, or publishing failed and waits for the developer. This happens
            // when a run is scheduled twice.
            return;
        }

        var publication = ReviewPublication.From(run);
        try
        {
            if (!await publisher.IsPublishedAsync(publication, cancellationToken))
            {
                await publisher.PublishAsync(publication, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run.FailPublishing(PublishingErrors.Failed(exception.Message), timeProvider.GetUtcNow());
            await repository.SaveAsync(run, cancellationToken);
            throw;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var comment in run.CommentsToPublish)
        {
            run.MarkCommentPublished(comment.Id, now);
        }

        run.CompletePublishing(now);
        await repository.SaveAsync(run, cancellationToken);
    }
}
