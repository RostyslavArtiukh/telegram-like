using MongoDB.Driver;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Infrastructure.Outbox;

internal interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> events,
        IClientSessionHandle session,
        CancellationToken ct = default);
}
