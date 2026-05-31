using MediatR;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Application.Queries.GetMessageById;

public sealed class GetMessageByIdQueryHandler(IMessageQueryService messageQueryService)
    : IRequestHandler<GetMessageByIdQuery, MessageDto?>
{
    public Task<MessageDto?> Handle(GetMessageByIdQuery request, CancellationToken cancellationToken)
        => messageQueryService.GetMessageByIdAsync(request.MessageId, request.RequesterId, cancellationToken);
}
