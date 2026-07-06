using MongoDB.Driver;
using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Infrastructure.Outbox;

namespace TelegramLike.Messaging.Infrastructure.Tests.Fixtures;

// Test double: the repository tests exercise Mongo persistence + optimistic
// concurrency, not the outbox. A no-op dispatcher keeps AddAsync/UpdateAsync's
// transaction happy without needing an outbox collection/consumer.
internal sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> events, IClientSessionHandle session, CancellationToken ct = default)
        => Task.CompletedTask;
}
