using ReviewAgent.Application.Reviews.Commands.EditComment;
using ReviewAgent.Application.Tests.Fakes;
using ReviewAgent.Domain;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Reviews.Errors;

namespace ReviewAgent.Application.Tests.Reviews.Commands.EditComment;

public sealed class EditCommentCommandHandlerTests
{
    private const string EditedBody = "Move this query to an application handler.";

    private readonly InMemoryReviewRunRepository _repository = new();

    private EditCommentCommandHandler Handler => new(_repository, new FixedTimeProvider(TestReviewRuns.Now));

    [Fact]
    public async Task Edited_comment_is_saved_with_the_new_body()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        await Handler.HandleAsync(new EditCommentCommand(runId, commentId, EditedBody), CancellationToken.None);

        var run = await _repository.FindAsync(new ReviewRunId(runId), CancellationToken.None);
        Assert.NotNull(run);
        var comment = Assert.Single(run.Comments);
        Assert.Equal(CommentDecision.Edited, comment.Decision);
        Assert.Equal(EditedBody, comment.Body);
        Assert.Equal(TestReviewRuns.CommentBody, comment.ProposedBody);
    }

    [Fact]
    public async Task Blank_body_is_rejected()
    {
        var (runId, commentId) = await TestReviewRuns.SaveFinishedRunWithOneCommentAsync(_repository);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => Handler.HandleAsync(new EditCommentCommand(runId, commentId, " "), CancellationToken.None));

        Assert.Equal(ReviewRunErrors.EditedBodyRequired, error.Message);
    }
}
