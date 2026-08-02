using TelegramLike.Contracts.Common;

namespace TelegramLike.Shared.Application;

/// <summary>
/// Splits an event's fan-out audience across as many wire messages as it takes, so the size of
/// one event stops growing with the size of the chat.
/// </summary>
/// <remarks>
/// Embedding the audience is deliberate — it is what keeps a consumer from reading another
/// service's data (see the cross-service rule) — but embedding it <i>whole</i> made one send
/// into a group of ten thousand a single ~400 KB row in the outbox and a single ~400 KB frame
/// on the broker, once per message. Splitting keeps the embed and drops the ceiling.
/// <para>
/// Each part is a complete event with its own id, and the slices are disjoint, so a consumer
/// that works per recipient needs no awareness of parts at all. Only a consumer doing something
/// once per <i>event</i> has to look at <c>PartIndex</c>.
/// </para>
/// </remarks>
public static class FanoutParts
{
    /// <summary>
    /// Recipients per wire message. A GUID costs ~39 bytes as JSON, so 500 of them is a ~20 KB
    /// event — small enough to stay well inside RabbitMQ's default 128 KB frame, large enough
    /// that ordinary chats are never split at all and pay nothing for this.
    /// </summary>
    public const int MaxPerEvent = 500;

    /// <summary>
    /// Builds one integration event per slice of <paramref name="audience"/>. <paramref name="build"/>
    /// receives the part's event id, its slice, its index and the total number of parts.
    /// </summary>
    /// <remarks>
    /// Part 0 reuses <paramref name="eventId"/> so an audience that fits in one message produces
    /// exactly the event it always did. Later parts get fresh ids on purpose: Notifications
    /// deduplicates by (recipient, source event), so parts sharing one id would be indistinguishable
    /// from a redelivery of the same part.
    /// <para>
    /// An empty audience still yields one event — an event whose audience happens to be nobody
    /// must still travel (a message in a chat with no other members is still a message).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<IIntegrationEvent> Split<T>(
        IReadOnlyList<T>? audience,
        Guid eventId,
        Func<Guid, IReadOnlyList<T>, int, int, IIntegrationEvent> build,
        int maxPerPart = MaxPerEvent)
    {
        var slices = Slice(audience, maxPerPart);

        return slices
            .Select((slice, index) => build(index == 0 ? eventId : Guid.NewGuid(), slice, index, slices.Count))
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<T>> Slice<T>(IReadOnlyList<T>? audience, int maxPerPart)
    {
        if (audience is null || audience.Count == 0) return [Array.Empty<T>()];
        if (audience.Count <= maxPerPart) return [audience];

        var slices = new List<IReadOnlyList<T>>();
        for (var start = 0; start < audience.Count; start += maxPerPart)
            slices.Add(audience.Skip(start).Take(maxPerPart).ToList());

        return slices;
    }
}
