using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ReviewAgent.Infrastructure.Storage;

/// <summary>Resolves the configured data directory to a full path.</summary>
internal static class DataDirectoryPath
{
    /// <summary>
    /// The full path of <see cref="StorageOptions.DataDirectory"/>: a relative setting, such as "data", resolved
    /// against the application's content root.
    /// </summary>
    // The content root, not the current directory, so the path does not depend on where the application was
    // started from.
    public static string Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        return Path.GetFullPath(options.DataDirectory, environment.ContentRootPath);
    }
}
