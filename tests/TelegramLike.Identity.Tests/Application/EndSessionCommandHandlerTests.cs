using NSubstitute;
using TelegramLike.Identity.Application.Commands.EndSession;
using TelegramLike.Identity.Application.Security;

namespace TelegramLike.Identity.Tests.Application;

public class EndSessionCommandHandlerTests
{
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();

    private EndSessionCommandHandler Handler => new(_sessionService);

    [Fact]
    public async Task EndSession_DeletesTheSessionToken()
    {
        await Handler.Handle(new EndSessionCommand("tok"), CancellationToken.None);

        await _sessionService.Received(1).DeleteSessionAsync("tok", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EndSession_EmptyOrWhitespaceToken_IsANoOp(string token)
    {
        // Nothing to revoke, and we must not fire a pointless store delete.
        await Handler.Handle(new EndSessionCommand(token), CancellationToken.None);

        await _sessionService.DidNotReceive().DeleteSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
