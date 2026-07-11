using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Application.Commands.StartTyping;

// Membership validation is back: the local read model populated from
// MemberJoined/Kicked/Left integration events tells us whether the caller
// is actually in this chat. Fail-open for unknown chats so chats created
// before this read model existed still work — once the read model has
// seen the chat, validation becomes strict. Backfill is the long-term fix.
//
// Day 17 publishes UserTypingIntegrationEvent so Web can push real-time
// notifications via Blazor circuit. Direct publish (no outbox) because
// typing is ephemeral — Redis TTL is 5s.
public sealed class StartTypingCommandHandler(
    ITypingIndicatorService typingService,
    IChatMembershipReadModel membership,
    IPublishEndpoint publishEndpoint,
    ILogger<StartTypingCommandHandler> logger)
    : IRequestHandler<StartTypingCommand>
{
    public async Task Handle(StartTypingCommand request, CancellationToken cancellationToken)
    {
        var isMember = await membership.IsActiveMemberAsync(request.ChatId, request.UserId, cancellationToken);
        if (!isMember)
        {
            // Fail-open: until the read model is fully backfilled with legacy chats,
            // we only refuse when we are sure the caller is NOT a member. We can
            // tighten this to fail-closed once a backfill has run.
            logger.LogWarning(
                "StartTyping: user {UserId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                request.UserId,
                request.ChatId);
        }

        await typingService.StartTypingAsync(request.ChatId, request.UserId, cancellationToken);

        await publishEndpoint.Publish(new UserTypingIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ChatId: request.ChatId,
            UserId: request.UserId), cancellationToken);
    }
}
