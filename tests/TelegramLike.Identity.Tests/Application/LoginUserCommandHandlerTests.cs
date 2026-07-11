using TelegramLike.Identity.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Identity.Application.Commands.LoginUser;
using TelegramLike.Identity.Application.Security;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;

namespace TelegramLike.Identity.Tests.Application;

public class LoginUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();

    private LoginUserCommandHandler Handler => new(_userRepository, _passwordHasher, _sessionService);

    private static User NewActiveUser(string email = "a@b.com", string password = "hashed")
        => User.Register(Guid.NewGuid(), email, "someuser", "Some User", password);

    [Fact]
    public async Task Unknown_email_throws_generic_invalid_credentials()
    {
        _userRepository.GetByEmailAsync(Arg.Any<TelegramLike.Identity.Domain.ValueObjects.Email>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var act = () => Handler.Handle(new LoginUserCommand("nobody@x.com", "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Wrong_password_throws_generic_invalid_credentials()
    {
        var user = NewActiveUser();
        _userRepository.GetByEmailAsync(Arg.Any<TelegramLike.Identity.Domain.ValueObjects.Email>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("wrong", user.Password.Hash).Returns(false);

        var act = () => Handler.Handle(new LoginUserCommand(user.Email.Value, "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Invalid email or password*");
        await _sessionService.DidNotReceive().CreateSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AccountStatus.Banned)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Non_active_account_is_rejected_even_with_correct_password(AccountStatus status)
    {
        var user = User.FromStorage(
            Guid.NewGuid(), "a@b.com", "someuser", "Some User", "hashed",
            avatarUrl: null, status: status, isPremium: false, premiumExpiresAt: null,
            blockedUserIds: [], createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow);
        _userRepository.GetByEmailAsync(Arg.Any<TelegramLike.Identity.Domain.ValueObjects.Email>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("correct", user.Password.Hash).Returns(true);

        var act = () => Handler.Handle(new LoginUserCommand(user.Email.Value, "correct"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not active*");
        await _sessionService.DidNotReceive().CreateSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Active_account_with_correct_password_creates_a_session()
    {
        var user = NewActiveUser();
        _userRepository.GetByEmailAsync(Arg.Any<TelegramLike.Identity.Domain.ValueObjects.Email>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("correct", user.Password.Hash).Returns(true);
        _sessionService.CreateSessionAsync(user.Id, Arg.Any<CancellationToken>()).Returns("session-token");

        var result = await Handler.Handle(new LoginUserCommand(user.Email.Value, "correct"), CancellationToken.None);

        result.Should().Be("session-token");
    }
}
