using ReviewAgent.Domain.Reviews;
using ReviewAgent.Domain.Rules;
using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.Errors;
using ReviewAgent.Infrastructure.Agents.Tools;

namespace ReviewAgent.Infrastructure.Tests.Agents;

public sealed class ReviewInstructionsTests
{
    [Fact]
    public void Prompts_are_built_into_the_application()
    {
        Assert.False(string.IsNullOrWhiteSpace(ReviewInstructions.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(ReviewInstructions.TaskPrompt));
        Assert.False(string.IsNullOrWhiteSpace(ReviewInstructions.VerifierSystemPrompt));
    }

    // The prompt refers to the tools and to propose_comment's answer by name; a typo in the prompt file would
    // make the agent misuse them without any error.
    [Theory]
    [InlineData("get_pull_request")]
    [InlineData("get_team_rules")]
    [InlineData("get_diff")]
    [InlineData("get_file")]
    [InlineData(ProposeCommentTool.Name)]
    [InlineData("Not proposed")]
    public void System_prompt_uses_the_names_the_code_uses(string name)
    {
        Assert.Contains(name, ReviewInstructions.SystemPrompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("get_team_rules")]
    [InlineData("get_diff")]
    [InlineData("get_file")]
    [InlineData(RejectCommentTool.Name)]
    [InlineData("Not rejected")]
    public void Verifier_system_prompt_uses_the_names_the_code_uses(string name)
    {
        Assert.Contains(name, ReviewInstructions.VerifierSystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_without_its_placeholder_fails_with_the_file_name()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReviewInstructions.FillPlaceholder("Double-check these comments.", "task.md", "{comments}", "- id: 1"));

        Assert.Equal(AgentErrors.PlaceholderMissing("task.md", "{comments}"), exception.Message);
    }

    [Fact]
    public void Verifier_task_lists_each_comment_with_its_id()
    {
        var run = ReviewRun.Start(ReviewRunId.New(), new PullRequestRef("test", "demo", 1), DateTimeOffset.UtcNow);
        var commentId = run.ProposeComment(
            new CodeLocation("src/Orders.Api/Program.cs", 12),
            new RuleId("ARCH-01"),
            Severity.Error,
            "Use an application handler.",
            suggestedFix: null,
            DateTimeOffset.UtcNow);

        var task = ReviewInstructions.VerifierTaskPrompt(run.Comments);

        Assert.Contains(
            $"- id: {commentId.Value}\n  file: src/Orders.Api/Program.cs, line: 12\n  rule: ARCH-01\n"
            + "  comment: Use an application handler.",
            task,
            StringComparison.Ordinal);
    }
}
