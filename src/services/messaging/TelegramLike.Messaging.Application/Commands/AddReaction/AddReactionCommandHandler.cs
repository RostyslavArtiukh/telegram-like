using MediatR;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.AddReaction;

public sealed class AddReactionCommandHandler(IMessageRepository messageRepository)
    : IRequestHandler<AddReactionCommand>
{
    public async Task Handle(AddReactionCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        // Membership check ("only active chat members can react") was here.
        // Trust the BFF for now; Phase 8 brings local membership read-model back.
        message.AddReaction(request.UserId, request.Emoji, request.ActorIsPremium);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
