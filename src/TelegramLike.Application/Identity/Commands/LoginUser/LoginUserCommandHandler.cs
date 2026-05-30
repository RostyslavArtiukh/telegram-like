using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Domain.Identity.ValueObjects;

namespace TelegramLike.Application.Identity.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISessionService sessionService)
    : IRequestHandler<LoginUserCommand, string>
{
    public async Task<string> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await userRepository.GetByEmailAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password.");

        if (!passwordHasher.Verify(request.Password, user.Password.Hash))
            throw new InvalidOperationException("Invalid email or password.");

        return await sessionService.CreateSessionAsync(user.Id, cancellationToken);
    }
}
