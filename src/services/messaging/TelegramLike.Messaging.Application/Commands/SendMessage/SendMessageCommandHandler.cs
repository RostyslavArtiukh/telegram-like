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
        // Phase 8: strict membership check, fail-open until the read model is
        // backfilled for legacy chats (created before this consumer existed).
        var isMember = await membership.IsActiveMemberAsync(request.ChatId, request.AuthorId, cancellationToken);
        if (!isMember)
        {
            logger.LogWarning(
                "SendMessage: author {AuthorId} is not in the local membership read-model for chat {ChatId}; allowing through (fail-open).",
                request.AuthorId,
                request.ChatId);
        }

        if (request.ReplyToMessageId.HasValue)
        {
            var replyTarget = await messageRepository.GetByIdAsync(request.ReplyToMessageId.Value, cancellationToken)
                              ?? throw new InvalidOperationException("Reply target message not found.");

            if (replyTarget.ChatId != request.ChatId)
                throw new InvalidOperationException("Cannot reply to a message from a different chat.");

            if (replyTarget.IsRetracted)
                throw new InvalidOperationException("Cannot reply to a retracted message.");
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
            request.Recipients,
            replyRef,
            forwardRef,
            isBroadcast: request.IsBroadcast);

        await messageRepository.AddAsync(message, cancellationToken);

        return message.Id;
    }
}
