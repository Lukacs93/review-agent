namespace ReviewAgent.Infrastructure.Persistence.Reviews.Errors;

/// <summary>The messages of the exceptions thrown when review events cannot be stored or read.</summary>
internal static class ReviewEventStoreErrors
{
    public static string UnknownEventType(string eventType) =>
        $"Unknown stored review event type: '{eventType}'.";

    public static string EmptyEventData(string eventType) =>
        $"The stored '{eventType}' event has no data.";

    public static string UnregisteredEvent(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return $"Review event {eventType.Name} has no stored type name. Register it in ReviewEventSerializer.";
    }
}
