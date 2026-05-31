using MediatR;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RetractMessage;

public sealed class RetractMessageCommandHandler(IMessageRepository messageRepository)
    : IRequestHandler<RetractMessageCommand>
{
    public async Task Handle(RetractMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var isAuthor = message.AuthorId == request.ActorUserId;

        // Author can always retract own. Moderator (Owner/Admin per chat role)
        // can retract anyone's — flag comes from the Web BFF since Messaging
        // can't query Chats anymore.
        message.Retract(request.ActorUserId, isAuthor || request.ActorIsModerator);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
