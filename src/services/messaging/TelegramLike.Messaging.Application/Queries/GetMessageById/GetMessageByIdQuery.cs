using MediatR;

namespace TelegramLike.Messaging.Application.Queries.GetMessageById;

public sealed record GetMessageByIdQuery(Guid MessageId, Guid RequesterId) : IRequest<MessageDto?>;
