using MongoDB.Driver;
using TelegramLike.Shared.Domain;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

// Test double: the repository tests exercise Mongo persistence + optimistic
// concurrency, not the outbox. A no-op dispatcher keeps AddAsync/UpdateAsync's
// transaction happy without needing an outbox collection/consumer.
internal sealed class NoOpOutgoingEventsWriter : IOutgoingEventsWriter
{
    public Task WriteAsync(IEnumerable<IChangeEvent> events, IClientSessionHandle session, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
