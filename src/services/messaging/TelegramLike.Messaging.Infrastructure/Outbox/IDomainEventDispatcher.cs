using MongoDB.Driver;
using TelegramLike.Messaging.Domain.Common;

namespace TelegramLike.Messaging.Infrastructure.Outbox;

internal interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default);
}
