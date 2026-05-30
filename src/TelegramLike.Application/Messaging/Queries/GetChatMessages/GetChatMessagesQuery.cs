using MediatR;

namespace TelegramLike.Application.Messaging.Queries.GetChatMessages;

public sealed record GetChatMessagesQuery(
    Guid ChatId,
    Guid RequesterId,
    DateTime? BeforeSentAt = null,
    int PageSize = 50) : IRequest<MessagePageDto>;
