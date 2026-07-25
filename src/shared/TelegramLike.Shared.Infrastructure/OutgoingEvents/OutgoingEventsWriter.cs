using System.Text.Json;
using MongoDB.Driver;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

public sealed class OutgoingEventsWriter : IOutgoingEventsWriter
{
    private readonly Dictionary<Type, IIntegrationEventMapper> _mappersByEventType;
    private readonly OutgoingEventsStore _store;

    public OutgoingEventsWriter(
        IEnumerable<IIntegrationEventMapper> mappers,
        OutgoingEventsStore store)
    {
        _mappersByEventType = mappers.ToDictionary(m => m.ChangeEventType);
        _store = store;
    }

    public async Task WriteAsync(
        IEnumerable<IChangeEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default)
    {
        var outgoing = new List<OutgoingEvent>();

        foreach (var changeEvent in events)
        {
            if (!_mappersByEventType.TryGetValue(changeEvent.GetType(), out var mapper))
                continue;

            var integrationEvent = mapper.Map(changeEvent);
            var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

            outgoing.Add(new OutgoingEvent(
                Id: Guid.NewGuid(),
                EventType: StableTypeName(integrationEvent.GetType()),
                Payload: payload,
                OccurredAt: changeEvent.OccurredAt));
        }

        if (outgoing.Count > 0)
            await _store.AddAsync(outgoing, session, cancellationToken);
    }

    // Store a version-agnostic "Namespace.Type, Assembly" name instead of the fully
    // version-qualified AssemblyQualifiedName. Type.GetType resolves both, but the
    // qualified form returns null for in-flight rows after an assembly version bump,
    // silently stranding them until they dead-letter.
    private static string StableTypeName(Type t) => $"{t.FullName}, {t.Assembly.GetName().Name}";
}
