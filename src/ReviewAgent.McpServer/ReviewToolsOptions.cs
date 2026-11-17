using System.ComponentModel.DataAnnotations;

namespace ReviewAgent.McpServer;

/// <summary>The <c>ReviewTools</c> section of the configuration.</summary>
internal sealed class ReviewToolsOptions
{
    public const string SectionName = "ReviewTools";

    /// <summary>Where the team keeps its review rules in the repository, relative to the repository root.</summary>
    [Required]
    public string TeamRulesPath { get; init; } = ".review/rules.md";
}
