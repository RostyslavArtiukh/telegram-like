using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed class SendMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership,
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
        var chatKnown = activeMembers.Count > 0;
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
            // still in flight). Fall back to the previous fail-open so a freshly created
            // chat's first send isn't rejected, and keep the caller-supplied recipients.
            logger.LogWarning(
                "SendMessage: chat {ChatId} is not in the membership read-model yet; allowing through (fail-open).",
                request.ChatId);
        }

        // Recipients are authoritative from the read-model when the chat is known — this
        // is what closes the recipient-spoofing vector; fall back to the caller's list
        // only while the chat is still unknown.
        var recipients = chatKnown
            ? activeMembers.Where(id => id != request.AuthorId).ToList()
            : request.Recipients;

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

        // Idempotency key: use the client-supplied id so a retried send collapses onto
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
            isBroadcast: request.IsBroadcast);

        await messageRepository.AddAsync(message, cancellationToken);

        return message.Id;
    }
}
