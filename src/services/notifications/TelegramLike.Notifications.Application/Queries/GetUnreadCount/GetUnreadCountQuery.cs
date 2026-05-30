using MediatR;

namespace TelegramLike.Notifications.Application.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid RecipientId) : IRequest<long>;
