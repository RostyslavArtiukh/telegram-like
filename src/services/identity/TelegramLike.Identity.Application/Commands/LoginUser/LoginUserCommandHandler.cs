using MediatR;
using TelegramLike.Identity.Application.Common.Interfaces;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Application.Commands.LoginUser;

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

        // A banned/deleted user must not be able to mint a session (the durable
        // credential that exchanges for access JWTs). The aggregate models the
        // status; enforce it at the auth boundary.
        if (user.Status != AccountStatus.Active)
            throw new InvalidOperationException("This account is not active.");

        return await sessionService.CreateSessionAsync(user.Id, cancellationToken);
    }
}
