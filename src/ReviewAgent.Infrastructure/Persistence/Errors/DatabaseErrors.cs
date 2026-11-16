namespace ReviewAgent.Infrastructure.Persistence.Errors;

/// <summary>The messages of the exceptions thrown when the database is not set up correctly.</summary>
internal static class DatabaseErrors
{
    public static string ConnectionStringMissing(string name) =>
        $"Connection string '{name}' is missing. Set ConnectionStrings:{name} in the configuration.";
}
