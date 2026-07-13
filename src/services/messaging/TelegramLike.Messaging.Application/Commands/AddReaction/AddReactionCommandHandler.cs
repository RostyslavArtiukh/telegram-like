using MediatR;
using TelegramLike.Messaging.Application;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.AddReaction;

public sealed class AddReactionCommandHandler(
    IMessageRepository messageRepository,
    IChatMembershipReadModel membership)
    : IRequestHandler<AddReactionCommand>
{
    public async Task Handle(AddReactionCommand request, CancellationToken cancellationToken)
    {
        // Reactions are highly concurrent (many users react to one message), so guard
        // the write with optimistic-concurrency retry: reload-mutate-save each attempt.
        await ConcurrencyRetry.ExecuteAsync(async () =>
        {
            var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                          ?? throw new DomainException("Message not found.");

            // Fail-closed ([TL-101]): the read-model is backfilled, so a non-member is a real
            // non-member (not just an unmaterialized chat) and must be refused, not logged through.
            if (!await membership.IsActiveMemberAsync(message.ChatId, request.UserId, cancellationToken))
                throw new ForbiddenException("You are not a member of this chat.");

            message.AddReaction(request.UserId, request.Emoji, request.UserIsPremium);
            await messageRepository.UpdateAsync(message, cancellationToken);
        });
    }
}
