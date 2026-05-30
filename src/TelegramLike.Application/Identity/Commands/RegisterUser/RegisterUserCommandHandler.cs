using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Identity.Aggregates;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Domain.Identity.ValueObjects;

namespace TelegramLike.Application.Identity.Commands.RegisterUser;

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
