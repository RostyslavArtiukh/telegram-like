using MediatR;

namespace TelegramLike.Application.Presence.Queries.GetUserPresence;

public sealed record GetUserPresenceQuery(Guid UserId) : IRequest<UserPresenceDto?>;
