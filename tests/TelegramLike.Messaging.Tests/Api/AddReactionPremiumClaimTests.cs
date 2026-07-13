using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ClearExtensions;
using TelegramLike.Messaging.Application.Commands.AddReaction;
using TelegramLike.Messaging.Tests.Api.Harness;

namespace TelegramLike.Messaging.Tests.Api;

/// <summary>
/// [TL-102]: the reaction's premium flag is read from the signed <c>premium</c> JWT claim,
/// never from the request body — a client cannot spoof premium to raise its reaction limit.
/// </summary>
public sealed class AddReactionPremiumClaimTests(MessagingApiFactory factory) : IClassFixture<MessagingApiFactory>
{
    private static readonly Guid MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private async Task<bool> CapturePremiumForTokenAsync(bool tokenIsPremium)
    {
        factory.Mediator.ClearSubstitute();
        factory.Mediator.Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>()).Returns(Unit.Value);

        var client = factory.CreateAuthenticatedClient(isPremium: tokenIsPremium);
        await client.PostAsync($"/messages/{MessageId}/reactions",
            JsonContent.Create(new { emoji = "Like" }));

        var call = factory.Mediator.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<AddReactionCommand>()
            .Single();
        return call.UserIsPremium;
    }

    [Fact]
    public async Task PremiumToken_SetsUserIsPremiumTrue()
        => (await CapturePremiumForTokenAsync(tokenIsPremium: true)).Should().BeTrue();

    [Fact]
    public async Task NonPremiumToken_SetsUserIsPremiumFalse()
        => (await CapturePremiumForTokenAsync(tokenIsPremium: false)).Should().BeFalse();
}
