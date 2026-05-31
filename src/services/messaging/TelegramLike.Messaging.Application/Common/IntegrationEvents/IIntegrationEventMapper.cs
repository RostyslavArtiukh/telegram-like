using TelegramLike.Contracts.Common;
using TelegramLike.Messaging.Domain.Common;

namespace TelegramLike.Messaging.Application.Common.IntegrationEvents;

public interface IIntegrationEventMapper
{
    Type DomainEventType { get; }

    IIntegrationEvent Map(IDomainEvent domainEvent);
}
