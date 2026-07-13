using MediatR;
using TelegramLike.Messaging.Application;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.RemoveReaction;

public sealed class RemoveReactionCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership)
    : IRequestHandler<RemoveReactionCommand>
{
    public async Task Handle(RemoveReactionCommand request, CancellationToken cancellationToken)
    {
        await ConcurrencyRetry.ExecuteAsync(async () =>
        {
            var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                          ?? throw new DomainException("Message not found.");

            // Fail-closed ([TL-101]): backfilled read-model makes a non-member authoritative.
            if (!await membership.IsActiveMemberAsync(message.ChatId, request.UserId, cancellationToken))
                throw new ForbiddenException("You are not a member of this chat.");

            message.RemoveReaction(request.UserId, request.Emoji);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
