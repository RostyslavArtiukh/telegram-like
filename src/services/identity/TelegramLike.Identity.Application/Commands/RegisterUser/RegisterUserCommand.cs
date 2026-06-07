using MediatR;

namespace TelegramLike.Identity.Application.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Username,
    string DisplayName,
    string Password) : IRequest<Guid>;
