using MediatR;

namespace TelegramLike.Identity.Application.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Username,
    string DisplayName,
    string Password,
    // Client-supplied user id = duplicate-protection key; empty => the handler mints one.
    Guid UserId = default) : IRequest<Guid>;
