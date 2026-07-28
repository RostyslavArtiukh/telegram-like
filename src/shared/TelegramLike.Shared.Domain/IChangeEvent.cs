namespace TelegramLike.Shared.Domain;

/// <summary>
/// "Something happened inside this service" — recorded by an <see cref="ObjectWithEvents"/>
/// when its state changes (member kicked, message sent, …). Stays private to the service;
/// events other services should hear about are produced from these by the service's
/// IntegrationEventMap and published through the outgoing-events queue.
/// </summary>
public interface IChangeEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
