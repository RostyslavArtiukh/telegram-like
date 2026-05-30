using MediatR;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Domain.Messaging.Repositories;

namespace TelegramLike.Application.Messaging.Commands.RetractMessage;

public sealed class RetractMessageCommandHandler(
    IMessageRepository messageRepository,
    IChatRepository chatRepository)
    : IRequestHandler<RetractMessageCommand>
{
    public async Task Handle(RetractMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var isAuthor = message.AuthorId == request.ActorUserId;
        var isModerator = false;

        if (!isAuthor)
        {
            var chat = await chatRepository.GetByIdAsync(message.ChatId, cancellationToken)
                       ?? throw new InvalidOperationException("Chat not found.");

            var actor = chat.FindActiveMember(request.ActorUserId)
                        ?? throw new InvalidOperationException("Actor is not an active member of this chat.");

            isModerator = actor.Role is MemberRole.Owner or MemberRole.Admin;
        }

        message.Retract(request.ActorUserId, isAuthor || isModerator);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
