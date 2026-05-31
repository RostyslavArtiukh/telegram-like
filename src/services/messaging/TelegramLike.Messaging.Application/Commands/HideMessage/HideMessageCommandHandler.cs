using MediatR;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Repositories;

namespace TelegramLike.Messaging.Application.Commands.HideMessage;

public sealed class HideMessageCommandHandler(
    IMessageRepository messageRepository,
    IHiddenMessageRepository hiddenMessageRepository)
    : IRequestHandler<HideMessageCommand>
{
    public async Task Handle(HideMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await messageRepository.GetByIdAsync(request.MessageId, cancellationToken)
                      ?? throw new InvalidOperationException("Message not found.");

        await hiddenMessageRepository.HideAsync(message.Id, request.UserId, cancellationToken);
    }
}
