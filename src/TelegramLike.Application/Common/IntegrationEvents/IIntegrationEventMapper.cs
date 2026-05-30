using TelegramLike.Contracts.Common;
using TelegramLike.Domain.Common;

namespace TelegramLike.Application.Common.IntegrationEvents;

public interface IIntegrationEventMapper
{
    Type DomainEventType { get; }

    IIntegrationEvent Map(IDomainEvent domainEvent);
}
