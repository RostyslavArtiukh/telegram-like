using FluentAssertions;
using NSubstitute;
using TelegramLike.Identity.Application.Auth.ExchangeSession;
using TelegramLike.Identity.Application.Security;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;

namespace TelegramLike.Identity.Tests.Application;

public class ExchangeSessionQueryHandlerTests
{
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAccessTokenIssuer _tokenIssuer = Substitute.For<IAccessTokenIssuer>();

    private ExchangeSessionQueryHandler Handler => new(_sessionService, _userRepository, _tokenIssuer);

    private static User NewUser(AccountStatus status = AccountStatus.Active)
        => User.FromStorage(
            Guid.NewGuid(), "a@b.com", "someuser", "Some User", "hashed",
            avatarUrl: null, status: status, isPremium: false, premiumExpiresAt: null,
            blockedUserIds: [], createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow);

    [Fact]
    public async Task Exchange_EmptySessionToken_ReturnsNull()
    {
        var result = await Handler.Handle(new ExchangeSessionQuery(""), CancellationToken.None);

        result.Should().BeNull();
        await _sessionService.DidNotReceive().GetUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exchange_UnknownSessionToken_ReturnsNull()
    {
        _sessionService.GetUserIdAsync("tok", Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var result = await Handler.Handle(new ExchangeSessionQuery("tok"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Exchange_SessionForDeletedUser_ReturnsNull()
    {
        var user = NewUser(AccountStatus.Deleted);
        _sessionService.GetUserIdAsync("tok", Arg.Any<CancellationToken>()).Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler.Handle(new ExchangeSessionQuery("tok"), CancellationToken.None);

        result.Should().BeNull();
        _tokenIssuer.DidNotReceive().IssueForUser(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Exchange_SessionForBannedUser_ReturnsNull()
    {
        var user = NewUser(AccountStatus.Banned);
        _sessionService.GetUserIdAsync("tok", Arg.Any<CancellationToken>()).Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Handler.Handle(new ExchangeSessionQuery("tok"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Exchange_ValidSessionForActiveUser_MintsAccessToken()
    {
        var user = NewUser();
        _sessionService.GetUserIdAsync("tok", Arg.Any<CancellationToken>()).Returns(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tokenIssuer.IssueForUser(user.Id).Returns(new AccessToken("jwt", 3600));

        var result = await Handler.Handle(new ExchangeSessionQuery("tok"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(user.Id);
        result.AccessToken.Should().Be("jwt");
        result.ExpiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task Exchange_UserDeletedAfterSessionCreated_ReturnsNull()
    {
        _sessionService.GetUserIdAsync("tok", Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Handler.Handle(new ExchangeSessionQuery("tok"), CancellationToken.None);

        result.Should().BeNull();
    }
}
