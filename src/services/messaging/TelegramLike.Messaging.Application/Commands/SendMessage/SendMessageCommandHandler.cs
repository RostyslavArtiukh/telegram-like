using MediatR;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed class SendMessageCommandHandler(IMessageRepository messageRepository)
    : IRequestHandler<SendMessageCommand, Guid>
{
    public async Task<Guid> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        // Membership / role / broadcast-permission checks have moved to the Web BFF.
        // Messaging trusts the JWT-authenticated caller; Phase 8 will reintroduce
        // strict validation via a local membership read model populated from
        // Chats integration events.
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

        var message = Message.Send(
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
