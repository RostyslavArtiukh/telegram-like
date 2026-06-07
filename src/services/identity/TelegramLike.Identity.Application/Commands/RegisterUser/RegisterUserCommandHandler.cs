using MediatR;
using TelegramLike.Identity.Application.Common.Interfaces;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Application.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var username = Username.Create(request.Username);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new InvalidOperationException($"Email '{request.Email}' is already taken.");

        if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
            throw new InvalidOperationException($"Username '{request.Username}' is already taken.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Register(request.Email, request.Username, request.DisplayName, passwordHash);

        await userRepository.AddAsync(user, cancellationToken);
        return user.Id;
    }
}
