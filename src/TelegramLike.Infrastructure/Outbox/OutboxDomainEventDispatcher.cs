using System.Text.Json;
using MongoDB.Driver;
using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Domain.Common;

namespace TelegramLike.Infrastructure.Outbox;

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
        CancellationToken ct = default)
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
                EventType: integrationEvent.GetType().AssemblyQualifiedName!,
                Payload: payload,
                OccurredAt: domainEvent.OccurredAt));
        }

        if (messages.Count > 0)
            await _outboxStore.AddAsync(messages, session, ct);
    }
}
