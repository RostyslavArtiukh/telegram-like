using MassTransit;
using MediatR;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Application.Commands.StartTyping;

// Membership validation: the local read model populated from MemberJoined/Kicked/Left
// integration events (and the one-time snapshot backfill) tells us whether the caller is
// actually in this chat. Fail-closed ([TL-101]): the read model is backfilled with legacy
// chats, so a non-member is authoritative and refused with a 403.
//
// Day 17 publishes UserTypingIntegrationEvent so Web can push real-time
// notifications via Blazor circuit. Direct publish (no outbox) because
// typing is ephemeral — Redis TTL is 5s.
public sealed class StartTypingCommandHandler(
    ITypingIndicatorService typingService,
    IChatMembershipReadModel membership,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<StartTypingCommand>
{
    public async Task Handle(StartTypingCommand request, CancellationToken cancellationToken)
    {
        if (!await membership.IsActiveMemberAsync(request.ChatId, request.UserId, cancellationToken))
            throw new ForbiddenException("You are not a member of this chat.");

        await typingService.StartTypingAsync(request.ChatId, request.UserId, cancellationToken);

        await publishEndpoint.Publish(new UserTypingIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ChatId: request.ChatId,
            UserId: request.UserId), cancellationToken);
    }
}
