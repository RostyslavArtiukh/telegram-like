using MediatR;
using TelegramLike.Identity.Application.Security;
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
        var userId = request.UserId == Guid.Empty ? Guid.NewGuid() : request.UserId;

        // Idempotent retry: if this user id already exists, return it without re-running
        // the email/username checks — those would wrongly report "already taken" for the
        // user's own retried registration.
        var existing = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var email = Email.Create(request.Email);
        var username = Username.Create(request.Username);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new DomainException($"Email '{request.Email}' is already taken.");

        if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
            throw new DomainException($"Username '{request.Username}' is already taken.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Register(userId, request.Email, request.Username, request.DisplayName, passwordHash);

        await userRepository.AddAsync(user, cancellationToken);
        return user.Id;
    }
}
