using MongoDB.Driver;
using TelegramLike.Domain.Common;

namespace TelegramLike.Infrastructure.Outbox;

internal interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> events,
        IClientSessionHandle session,
        CancellationToken ct = default);
}
