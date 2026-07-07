using System.Text.Json;
using MongoDB.Driver;
using TelegramLike.Chats.Application.Common.IntegrationEvents;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Infrastructure.Outbox;

internal sealed class OutboxDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly Dictionary<Type, IIntegrationEventMapper> _mappers;
    private readonly IOutboxStore _outboxStore;

    public OutboxDomainEventDispatcher(
        IEnumerable<IIntegrationEventMapper> mappers,
        IOutboxStore outboxStore)
    {
        _mappers = mappers.ToDictionary(m => m.DomainEventType);
        _outboxStore = outboxStore;
    }

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<OutboxMessage>();

        foreach (var domainEvent in events)
        {
            if (!_mappers.TryGetValue(domainEvent.GetType(), out var mapper))
                continue;

            var integrationEvent = mapper.Map(domainEvent);
            var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

            messages.Add(new OutboxMessage(
                Id: Guid.NewGuid(),
                EventType: StableTypeName(integrationEvent.GetType()),
                Payload: payload,
                OccurredAt: domainEvent.OccurredAt));
        }

        if (messages.Count > 0)
            await _outboxStore.AddAsync(messages, session, cancellationToken);
    }

    // Store a version-agnostic "Namespace.Type, Assembly" name instead of the fully
    // version-qualified AssemblyQualifiedName. Type.GetType resolves both, but the
    // qualified form returns null for in-flight rows after an assembly version bump,
    // silently stranding them until they dead-letter.
    private static string StableTypeName(Type t) => $"{t.FullName}, {t.Assembly.GetName().Name}";
}
