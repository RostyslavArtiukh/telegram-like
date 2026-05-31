using TelegramLike.Chats.Domain.Common;
using TelegramLike.Contracts.Common;

namespace TelegramLike.Chats.Application.Common.IntegrationEvents;

public interface IIntegrationEventMapper
{
    Type DomainEventType { get; }

    IIntegrationEvent Map(IDomainEvent domainEvent);
}
