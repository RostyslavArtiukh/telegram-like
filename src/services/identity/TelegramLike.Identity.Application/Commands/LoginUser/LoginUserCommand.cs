using MediatR;

namespace TelegramLike.Identity.Application.Commands.LoginUser;

public sealed record LoginUserCommand(string Email, string Password) : IRequest<string>;
