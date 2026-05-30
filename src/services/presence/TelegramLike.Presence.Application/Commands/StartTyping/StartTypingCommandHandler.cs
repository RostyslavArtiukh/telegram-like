using MassTransit;
using MediatR;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Application.Commands.StartTyping;

// Chat membership check was dropped during the microservice extraction (Day 15) —
// Presence-service has no cross-context access to IChatRepository. JWT-authenticated
// caller is currently trusted. To restore strict validation, subscribe to
// MemberJoined/Left/Kicked integration events and maintain a local read model.
//
// Day 17: also publishes UserTypingIntegrationEvent so Web can push real-time
// notifications to other chat members via Blazor SignalR circuit. Direct publish
// (no outbox) because typing is ephemeral — Redis TTL is 5s, so a lost event just
// means slightly delayed UI; not worth a transaction for best-effort signal.
public sealed class StartTypingCommandHandler(
    ITypingIndicatorService typingService,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<StartTypingCommand>
{
    public async Task Handle(StartTypingCommand request, CancellationToken cancellationToken)
    {
        await typingService.StartTypingAsync(request.ChatId, request.UserId, cancellationToken);

        await publishEndpoint.Publish(new UserTypingIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ChatId: request.ChatId,
            UserId: request.UserId), cancellationToken);
    }
}
