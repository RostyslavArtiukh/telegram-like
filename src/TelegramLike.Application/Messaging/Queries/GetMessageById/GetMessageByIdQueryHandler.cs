using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Messaging.Queries.GetMessageById;

public sealed class GetMessageByIdQueryHandler(IMessageQueryService messageQueryService)
    : IRequestHandler<GetMessageByIdQuery, MessageDto?>
{
    public Task<MessageDto?> Handle(GetMessageByIdQuery request, CancellationToken cancellationToken)
        => messageQueryService.GetMessageByIdAsync(request.MessageId, request.RequesterId, cancellationToken);
}
