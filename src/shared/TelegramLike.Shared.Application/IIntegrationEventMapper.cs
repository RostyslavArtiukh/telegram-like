using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Application;

/// <summary>
/// Turns one service-internal <see cref="IChangeEvent"/> into the integration event
/// (from Contracts) that other services are allowed to see. One mapper per change-event
/// type; the outgoing-events writer picks the right one by <see cref="ChangeEventType"/>.
/// </summary>
public interface IIntegrationEventMapper
{
    Type ChangeEventType { get; }

    IIntegrationEvent Map(IChangeEvent changeEvent);
}
