using MediatR;

namespace TelegramLike.Presence.Application.Queries.GetUserPresence;

public sealed record GetUserPresenceQuery(Guid UserId) : IRequest<UserPresenceDto?>;
