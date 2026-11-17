using ReviewAgent.Infrastructure.Agents;
using ReviewAgent.Infrastructure.Agents.Tools;

namespace ReviewAgent.Infrastructure.Tests.Agents;

public sealed class ReviewInstructionsTests
{
    [Fact]
    public void Prompts_are_built_into_the_application()
    {
        Assert.False(string.IsNullOrWhiteSpace(ReviewInstructions.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(ReviewInstructions.TaskPrompt));
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
}
