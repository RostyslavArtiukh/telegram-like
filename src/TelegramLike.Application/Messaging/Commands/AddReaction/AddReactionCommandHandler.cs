using MediatR;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Domain.Messaging.Repositories;

namespace TelegramLike.Application.Messaging.Commands.AddReaction;

public sealed class AddReactionCommandHandler(
    IMessageRepository messageRepository,
    IChatRepository chatRepository,
    IUserRepository userRepository)
    : IRequestHandler<AddReactionCommand>
{
    public async Task Handle(AddReactionCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        var chat = await chatRepository.GetByIdAsync(message.ChatId, cancellationToken)
                   ?? throw new InvalidOperationException("Chat not found.");

        if (chat.FindActiveMember(request.UserId) is null)
            throw new InvalidOperationException("Only active chat members can react to messages.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
                   ?? throw new InvalidOperationException("User not found.");

        user.CheckPremiumExpiry();

        message.AddReaction(request.UserId, request.Emoji, user.IsPremium);
        await messageRepository.UpdateAsync(message, cancellationToken);
    }
}
