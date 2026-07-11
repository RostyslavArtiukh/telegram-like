using TelegramLike.Identity.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Identity.Application.Commands.RegisterUser;
using TelegramLike.Identity.Application.Security;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Application.Tests;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private RegisterUserCommandHandler Handler => new(_userRepository, _passwordHasher);

    [Fact]
    public async Task Idempotent_retry_with_an_existing_user_id_returns_that_id_without_uniqueness_checks()
    {
        var existingId = Guid.NewGuid();
        var existing = User.Register(existingId, "a@b.com", "someuser", "Some User", "hash");
        _userRepository.GetByIdAsync(existingId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Handler.Handle(
            new RegisterUserCommand("different@b.com", "differentuser", "Different", "pw", existingId),
            CancellationToken.None);

        result.Should().Be(existingId);
        await _userRepository.DidNotReceive().ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_email_throws()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => Handler.Handle(
            new RegisterUserCommand("taken@b.com", "newuser", "New User", "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already taken*");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_username_throws()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.ExistsByUsernameAsync(Arg.Any<Username>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => Handler.Handle(
            new RegisterUserCommand("new@b.com", "takenuser", "New User", "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already taken*");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fresh_registration_hashes_password_and_persists_the_user()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.ExistsByUsernameAsync(Arg.Any<Username>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("plaintext").Returns("hashed-value");

        User? captured = null;
        _userRepository.AddAsync(Arg.Do<User>(u => captured = u), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await Handler.Handle(
            new RegisterUserCommand("new@b.com", "newuser", "New User", "plaintext"), CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        captured!.Password.Hash.Should().Be("hashed-value");
        captured.Email.Value.Should().Be("new@b.com");
        captured.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task Empty_UserId_mints_a_new_one()
    {
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.ExistsByUsernameAsync(Arg.Any<Username>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hash");

        var result = await Handler.Handle(
            new RegisterUserCommand("new@b.com", "newuser", "New User", "pw", Guid.Empty), CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
    }
}
