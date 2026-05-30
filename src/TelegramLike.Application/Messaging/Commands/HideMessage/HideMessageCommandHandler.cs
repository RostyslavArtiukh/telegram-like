using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Messaging.Repositories;

namespace TelegramLike.Application.Messaging.Commands.HideMessage;

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
