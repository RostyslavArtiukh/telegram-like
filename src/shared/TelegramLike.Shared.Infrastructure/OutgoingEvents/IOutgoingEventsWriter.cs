using MongoDB.Driver;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Called by repositories while saving an aggregate: takes the aggregate's pending
/// change events and, inside the same Mongo transaction, writes their integration-event
/// form into the outgoing queue. An interface so repository tests can plug in a no-op.
/// </summary>
public interface IOutgoingEventsWriter
{
    Task WriteAsync(
        IEnumerable<IChangeEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default);
}
