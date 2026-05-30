using MediatR;

namespace TelegramLike.Application.Messaging.Queries.GetMessageById;

public sealed record GetMessageByIdQuery(Guid MessageId, Guid RequesterId) : IRequest<MessageDto?>;
