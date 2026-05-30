using MediatR;
using TelegramLike.Domain.Messaging.Repositories;

namespace TelegramLike.Application.Messaging.Commands.RemoveReaction;

public sealed class RemoveReactionCommandHandler(IMessageRepository messageRepository)
    : IRequestHandler<RemoveReactionCommand>
{
    public async Task Handle(RemoveReactionCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        message.RemoveReaction(request.UserId, request.Emoji);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
