using System.ComponentModel.DataAnnotations;
using ReviewAgent.Infrastructure.SourceControl.Saved.Errors;

namespace ReviewAgent.Infrastructure.SourceControl;

/// <summary>
/// The <c>PullRequests</c> section of the configuration: where the reviewed pull requests come from.
/// Checked when the application starts, so a missing setting stops it right away.
/// </summary>
internal sealed class PullRequestSourceOptions
{
    public const string SectionName = "PullRequests";

    /// <summary>
    /// The directory of the saved pull requests. A relative path is relative to the application's own directory,
    /// where the build copies the <c>samples</c> folder.
    /// </summary>
    [Required(ErrorMessage = SavedPullRequestErrors.DirectoryRequired)]
    public string SavedDirectory { get; init; } = string.Empty;
}
