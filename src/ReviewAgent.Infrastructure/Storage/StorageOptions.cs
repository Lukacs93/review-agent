using System.ComponentModel.DataAnnotations;
using ReviewAgent.Infrastructure.Storage.Errors;

namespace ReviewAgent.Infrastructure.Storage;

/// <summary>
/// The <c>Storage</c> section of the configuration: where the application writes its own data, the database and
/// the reviews published without GitHub.
/// </summary>
internal sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// The data directory. A relative path is relative to the application's content root: the project folder
    /// in development, the same folder <c>appsettings.json</c> is read from.
    /// </summary>
    // Never the application's own output folder: a rebuild or "dotnet clean" may delete what is written there.
    [Required(ErrorMessage = StorageErrors.DataDirectoryRequired)]
    public string DataDirectory { get; init; } = string.Empty;
}
