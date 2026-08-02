using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Observability;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed class SendMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
    IChatTypeReadModel chatType,
    MessagingMetrics metrics,
    ILogger<SendMessageCommandHandler> logger)
    : IRequestHandler<SendMessageCommand, Guid>
{
    public async Task<Guid> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        // One read-model query drives three things: the membership check, the
        // authoritative recipient list (no longer trusting the caller), and whether
        // the chat is materialized at all. The read-model is event-sourced from Chats
        // (MemberJoined/Kicked/Left).
        var activeMembers = await membership.GetActiveMemberIdsAsync(request.ChatId, cancellationToken);

        // "No active members" alone does NOT mean the chat is unknown — a deleted chat (or one
        // whose members were all banned) is materialized here with every row deactivated.
        // Treating that as unknown would drop straight into the fail-open branch below and let
        // anyone post into a chat that no longer accepts anything. The extra lookup only runs in
        // that ambiguous case, since a non-empty active set already proves the chat is known.
        var chatKnown = activeMembers.Count > 0
                        || await membership.IsChatKnownAsync(request.ChatId, cancellationToken);
        var isMember = activeMembers.Contains(request.AuthorId);

        if (chatKnown && !isMember)
        {
            // The chat is materialized and the author is not in it → a genuine
            // non-member. Fail closed (→ 403 via DomainExceptionFilter).
            throw new ForbiddenException("You are not an active member of this chat.");
        }

        if (!chatKnown)
        {
            // Chat not yet materialized (legacy chat, or the creator's MemberJoined is
            // still in flight). Fail open on membership so a freshly created chat's first
            // send isn't rejected — but with nobody to fan out to, see below.
            logger.LogWarning(
                "SendMessage: chat {ChatId} is not in the membership read-model yet; storing the message " +
                "(fail-open) but fanning out to nobody — its audience is unknown until MemberJoined arrives.",
                request.ChatId);
        }

        // The read-model is the only source of recipients ([TL-118]). It used to fall back to a
        // caller-supplied list for an unmaterialized chat, which meant the same "who is in this
        // chat" derivation lived here AND in each UI host — two copies that could disagree, and
        // a spoofing vector whenever the fallback engaged. An unknown chat now simply has no
        // known audience: the message is stored and readable, it just raises no notification or
        // realtime push, for the couple of seconds until Chats' MemberJoined lands.
        var recipients = activeMembers.Where(id => id != request.AuthorId).ToList();

        // Broadcast-ness is authoritative from the chat-type read-model when the chat is
        // materialized ([TL-102]); the client-supplied flag is used only for a not-yet-known
        // chat (same first-send race as recipients), then ignored once the type is materialized.
        var isBroadcast = await chatType.IsBroadcastAsync(request.ChatId, cancellationToken)
                          ?? request.IsBroadcast;

        if (request.ReplyToMessageId.HasValue)
        {
            var replyTarget = await messageRepository.GetByIdAsync(request.ReplyToMessageId.Value, cancellationToken)
                              ?? throw new DomainException("Reply target message not found.");

            if (replyTarget.ChatId != request.ChatId)
                throw new DomainException("Cannot reply to a message from a different chat.");

            if (replyTarget.IsRetracted)
                throw new DomainException("Cannot reply to a retracted message.");
        }

        var attachments = request.Attachments?
            .Select(a => Attachment.Create(a.Type, a.Url, a.SizeBytes, a.FileName))
            .ToList() ?? [];

        var content = MessageContent.Create(request.Text, attachments);

        var replyRef = request.ReplyToMessageId.HasValue
            ? ReplyReference.To(request.ReplyToMessageId.Value)
            : null;

        var forwardRef = request.ForwardOriginalMessageId.HasValue && request.ForwardOriginalChatId.HasValue
            ? ForwardReference.From(request.ForwardOriginalMessageId.Value, request.ForwardOriginalChatId.Value)
            : null;

        // Duplicate protection: use the client-supplied id so a retried send collapses onto
        // the same document; mint one only if the caller didn't provide it.
        var messageId = request.MessageId == Guid.Empty ? Guid.NewGuid() : request.MessageId;

        var message = Message.Send(
            messageId,
            request.ChatId,
            request.AuthorId,
            content,
            recipients,
            replyRef,
            forwardRef,
            isBroadcast: isBroadcast);

        await messageRepository.AddAsync(message, cancellationToken);

        var kind = forwardRef is not null ? "forward" : replyRef is not null ? "reply" : "new";
        metrics.RecordMessageSent(isBroadcast, kind);

        return message.Id;
    }
}
