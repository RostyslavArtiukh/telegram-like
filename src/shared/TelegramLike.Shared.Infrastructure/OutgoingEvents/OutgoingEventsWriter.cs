using System.Text.Json;
using MongoDB.Driver;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

public sealed class OutgoingEventsWriter(
    IntegrationEventMap map,
    OutgoingEventsStore store) : IOutgoingEventsWriter
{
    public async Task WriteAsync(
        IEnumerable<IChangeEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default)
    {
        var outgoing = new List<OutgoingEvent>();

        foreach (var changeEvent in events)
        {
            // A null result means the service deliberately keeps this event internal —
            // the service's map is the single place that decides, and its default arm is
            // reviewed there rather than depending on a DI registration being present.
            var integrationEvent = map(changeEvent);
            if (integrationEvent is null) continue;

            var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

            outgoing.Add(new OutgoingEvent(
                Id: Guid.NewGuid(),
                // The event's declared wire name, not its CLR name: a queued row outlives the
                // build that wrote it, so it must not depend on the class keeping its name or
                // namespace. See IntegrationEventNames.
                EventType: IntegrationEventNames.NameOf(integrationEvent.GetType()),
                Payload: payload,
                OccurredAt: changeEvent.OccurredAt));
        }

        if (outgoing.Count > 0)
            await store.AddAsync(outgoing, session, cancellationToken);
    }
}
