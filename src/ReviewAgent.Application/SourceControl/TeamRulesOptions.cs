using System.ComponentModel.DataAnnotations;
using ReviewAgent.Application.SourceControl.Errors;

namespace ReviewAgent.Application.SourceControl;

/// <summary>Where the team keeps its review rules in the reviewed repository.</summary>
// Read by both the API, which checks the model's comments against the rules, and the MCP server, which gives the
// rules to the model; one setting, so the two always read the same file.
public sealed class TeamRulesOptions
{
    /// <summary>The configuration section: <c>PullRequests:TeamRulesPath</c>.</summary>
    public const string SectionName = "PullRequests";

    /// <summary>The rules file, relative to the repository root.</summary>
    [Required(ErrorMessage = SourceControlErrors.TeamRulesPathRequired)]
    public string TeamRulesPath { get; init; } = ".review/rules.md";
}
