using MediatR;

namespace TelegramLike.Identity.Application.Queries.GetUserIdByUsername;

/// <summary>
/// Resolves a username to its user id. Returns <c>null</c> when the username is
/// malformed or no such user exists — callers (e.g. the "start a direct chat by
/// username" flow) treat both cases as "user not found".
/// </summary>
public sealed record GetUserIdByUsernameQuery(string Username) : IRequest<Guid?>;
