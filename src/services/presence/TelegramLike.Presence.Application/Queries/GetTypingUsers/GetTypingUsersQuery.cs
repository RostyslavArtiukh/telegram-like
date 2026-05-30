using MediatR;

namespace TelegramLike.Presence.Application.Queries.GetTypingUsers;

public sealed record GetTypingUsersQuery(Guid ChatId) : IRequest<TypingUsersDto>;
