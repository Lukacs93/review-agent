using ReviewAgent.Evaluation.Grading;
using static ReviewAgent.Evaluation.Tests.TestReviews;

namespace ReviewAgent.Evaluation.Tests.Grading;

public sealed class ReviewGraderTests
{
    [Fact]
    public void Comment_on_one_of_the_lines_finds_the_finding()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13, 39)],
            [Comment("ARCH-01", ControllerFile, 39)]);

        Assert.Equal(CommentVerdict.Hit, Assert.Single(graded.Comments).Verdict);
        Assert.True(Assert.Single(graded.FoundFindings));
    }

    [Fact]
    public void Rule_is_compared_without_case()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13)],
            [Comment("arch-01", ControllerFile, 13)]);

        Assert.Equal(CommentVerdict.Hit, Assert.Single(graded.Comments).Verdict);
    }

    [Fact]
    public void Comment_with_the_rule_on_another_line_is_in_the_wrong_place()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13)],
            [Comment("ARCH-01", ControllerFile, 20)]);

        Assert.Equal(CommentVerdict.WrongPlace, Assert.Single(graded.Comments).Verdict);
        Assert.False(Assert.Single(graded.FoundFindings));
    }

    // The CI run's mistake: the missing test pointed out as DDD-01 instead of TEST-01.
    [Fact]
    public void Comment_on_the_line_with_another_rule_has_the_wrong_rule()
    {
        var graded = ReviewGrader.Grade(
            [Finding("TEST-01", OrderFile, false, 50)],
            [Comment("DDD-01", OrderFile, 50)]);

        Assert.Equal(CommentVerdict.WrongRule, Assert.Single(graded.Comments).Verdict);
        Assert.False(Assert.Single(graded.FoundFindings));
    }

    [Fact]
    public void Second_comment_on_a_found_finding_is_a_duplicate()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13, 39)],
            [Comment("ARCH-01", ControllerFile, 13), Comment("ARCH-01", ControllerFile, 39)]);

        Assert.Equal(CommentVerdict.Hit, graded.Comments[0].Verdict);
        Assert.Equal(CommentVerdict.Duplicate, graded.Comments[1].Verdict);
    }

    [Fact]
    public void Comment_the_pull_request_does_not_call_for_is_false()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13)],
            [Comment("SEC-02", OrderFile, 5)]);

        Assert.Equal(CommentVerdict.False, Assert.Single(graded.Comments).Verdict);
    }

    [Fact]
    public void Hit_does_not_depend_on_the_order_of_the_comments()
    {
        var graded = ReviewGrader.Grade(
            [Finding("ARCH-01", ControllerFile, false, 13)],
            [Comment("ARCH-01", ControllerFile, 20), Comment("ARCH-01", ControllerFile, 13)]);

        Assert.Equal(CommentVerdict.Duplicate, graded.Comments[0].Verdict);
        Assert.Equal(CommentVerdict.Hit, graded.Comments[1].Verdict);
    }

    [Fact]
    public void Optional_finding_counts_when_found_and_is_no_mistake_when_missed()
    {
        var graded = ReviewGrader.Grade(
            [Finding("DDD-02", OrderFile, false, 6), Finding("NAME-01", OrderFile, true, 3)],
            [Comment("NAME-01", OrderFile, 3)]);

        Assert.Equal(CommentVerdict.Hit, Assert.Single(graded.Comments).Verdict);
        Assert.False(graded.FoundFindings[0]);
        Assert.True(graded.FoundFindings[1]);
    }

    [Fact]
    public void Required_finding_is_found_before_an_optional_one_on_the_same_line()
    {
        var graded = ReviewGrader.Grade(
            [Finding("NAME-01", OrderFile, true, 5), Finding("NAME-01", OrderFile, false, 5)],
            [Comment("NAME-01", OrderFile, 5)]);

        Assert.False(graded.FoundFindings[0]);
        Assert.True(graded.FoundFindings[1]);
    }
}
