using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Domain.Tests.Reviews;

public sealed class IdentifierTests
{
    [Fact]
    public void New_review_run_ids_are_unique()
    {
        Assert.NotEqual(ReviewRunId.New(), ReviewRunId.New());
    }

    [Fact]
    public void New_comment_ids_are_unique()
    {
        Assert.NotEqual(CommentId.New(), CommentId.New());
    }
}
