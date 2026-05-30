using MediatR;

namespace TelegramLike.Application.Presence.Queries.GetTypingUsers;

public sealed record GetTypingUsersQuery(Guid ChatId) : IRequest<TypingUsersDto>;
