using MediatR;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RemoveReaction;

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
