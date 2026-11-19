namespace ReviewAgent.Infrastructure.Storage.Errors;

/// <summary>The messages of the exceptions thrown when the data directory is not set up.</summary>
internal static class StorageErrors
{
    public const string DataDirectoryRequired =
        "Storage:DataDirectory is required: the directory the application writes its database and files to.";
}
