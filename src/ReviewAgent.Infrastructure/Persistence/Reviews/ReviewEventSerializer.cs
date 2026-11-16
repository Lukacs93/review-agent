using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewAgent.Domain.Reviews.Events;
using ReviewAgent.Infrastructure.Persistence.Reviews.Errors;

namespace ReviewAgent.Infrastructure.Persistence.Reviews;

/// <summary>Converts review events to and from the JSON stored in the event store.</summary>
public static class ReviewEventSerializer
{
    // The names are part of the stored data, so they are written out instead of taken from the class names:
    // renaming an event class must not make the events already stored unreadable.
    private static readonly Dictionary<string, Type> _typesByName = new(StringComparer.Ordinal)
    {
        ["ReviewStarted"] = typeof(ReviewStarted),
        ["ToolCalled"] = typeof(ToolCalled),
        ["CommentProposed"] = typeof(CommentProposed),
        ["ReviewFinished"] = typeof(ReviewFinished),
        ["ReviewFailed"] = typeof(ReviewFailed),
        ["CommentApproved"] = typeof(CommentApproved),
        ["CommentEdited"] = typeof(CommentEdited),
        ["CommentDiscarded"] = typeof(CommentDiscarded),
        ["CommentPublished"] = typeof(CommentPublished),
        ["ReviewPublished"] = typeof(ReviewPublished),
    };

    private static readonly Dictionary<Type, string> _namesByType =
        _typesByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string TypeNameOf(ReviewEvent reviewEvent)
    {
        ArgumentNullException.ThrowIfNull(reviewEvent);

        return _namesByType.TryGetValue(reviewEvent.GetType(), out var name)
            ? name
            : throw new InvalidOperationException(ReviewEventStoreErrors.UnregisteredEvent(reviewEvent.GetType()));
    }

    public static string Serialize(ReviewEvent reviewEvent)
    {
        ArgumentNullException.ThrowIfNull(reviewEvent);

        return JsonSerializer.Serialize(reviewEvent, reviewEvent.GetType(), _options);
    }

    public static ReviewEvent Deserialize(string eventType, string data)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(data);

        if (!_typesByName.TryGetValue(eventType, out var type))
        {
            throw new InvalidOperationException(ReviewEventStoreErrors.UnknownEventType(eventType));
        }

        return JsonSerializer.Deserialize(data, type, _options) as ReviewEvent
            ?? throw new InvalidOperationException(ReviewEventStoreErrors.EmptyEventData(eventType));
    }
}
