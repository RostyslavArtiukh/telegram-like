using MediatR;

namespace TelegramLike.Application.Identity.Commands.LoginUser;

public sealed record LoginUserCommand(string Email, string Password) : IRequest<string>;
