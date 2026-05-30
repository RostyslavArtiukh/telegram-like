using MediatR;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Messaging.Aggregates;
using TelegramLike.Domain.Messaging.Repositories;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Messaging.Commands.SendMessage;

public sealed class SendMessageCommandHandler(
    IChatRepository chatRepository,
    IMessageRepository messageRepository)
    : IRequestHandler<SendMessageCommand, Guid>
{
    public async Task<Guid> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var chat = await chatRepository.GetByIdAsync(request.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        if (chat.IsDeleted)
            throw new InvalidOperationException("Cannot send a message to a deleted chat.");

        var author = chat.FindActiveMember(request.AuthorId)
                     ?? throw new InvalidOperationException("Author is not an active member of this chat.");

        if (chat.Type == ChatType.Broadcast && author.Role is not (MemberRole.Owner or MemberRole.Admin))
            throw new InvalidOperationException("Only Owner or Admin can send messages in a BroadcastChannel.");

        if (request.ReplyToMessageId.HasValue)
        {
            var replyTarget = await messageRepository.GetByIdAsync(request.ReplyToMessageId.Value, cancellationToken)
                              ?? throw new InvalidOperationException("Reply target message not found.");

            if (replyTarget.ChatId != chat.Id)
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

        var recipients = chat.ActiveMembers
            .Where(m => m.UserId != author.UserId)
            .Select(m => m.UserId)
            .ToList();

        var message = Message.Send(
            chat.Id,
            author.UserId,
            content,
            recipients,
            replyRef,
            forwardRef,
            isBroadcast: chat.Type == ChatType.Broadcast);

        await messageRepository.AddAsync(message, cancellationToken);

        return message.Id;
    }
}
