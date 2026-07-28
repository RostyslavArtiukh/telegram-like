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
                EventType: StableTypeName(integrationEvent.GetType()),
                Payload: payload,
                OccurredAt: changeEvent.OccurredAt));
        }

        if (outgoing.Count > 0)
            await store.AddAsync(outgoing, session, cancellationToken);
    }

    // Store a version-agnostic "Namespace.Type, Assembly" name instead of the fully
    // version-qualified AssemblyQualifiedName. Type.GetType resolves both, but the
    // qualified form returns null for in-flight rows after an assembly version bump,
    // silently stranding them until they dead-letter.
    private static string StableTypeName(Type t) => $"{t.FullName}, {t.Assembly.GetName().Name}";
}
