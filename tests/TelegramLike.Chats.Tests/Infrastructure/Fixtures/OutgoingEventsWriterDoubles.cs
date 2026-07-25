using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure.Fixtures;

// Captures the change events the repository drains into the outbox, so tests can
// assert what was handed over without standing up the real outbox collection.
internal sealed class RecordingOutgoingEventsWriter : IOutgoingEventsWriter
{
    public List<IChangeEvent> Written { get; } = [];

    public Task WriteAsync(IEnumerable<IChangeEvent> events, IClientSessionHandle session, CancellationToken cancellationToken = default)
    {
        Written.AddRange(events);
        return Task.CompletedTask;
    }
}

// Simulates an outbox failure inside the transaction: the repository's chat and
// member writes must roll back with it.
internal sealed class ThrowingOutgoingEventsWriter : IOutgoingEventsWriter
{
    public Task WriteAsync(IEnumerable<IChangeEvent> events, IClientSessionHandle session, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("outbox write failed");
}
