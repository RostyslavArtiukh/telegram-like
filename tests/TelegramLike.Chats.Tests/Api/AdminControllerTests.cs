using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using TelegramLike.Chats.Application.Commands.BackfillChatMemberships;
using TelegramLike.Chats.Tests.Api.Harness;

namespace TelegramLike.Chats.Tests.Api;

/// <summary>
/// The admin backfill endpoint: authenticated ([Authorize]) and gated behind
/// <c>Admin:BackfillEnabled</c> so the surface stays hidden (404) unless deliberately enabled.
/// </summary>
public sealed class AdminControllerTests(ChatsApiFactory factory) : IClassFixture<ChatsApiFactory>
{
    private const string Path = "/admin/backfill/chat-memberships";

    private static HttpContent Empty() => new StringContent("");

    [Fact]
    public async Task Anonymous_Returns401()
    {
        var response = await factory.CreateClient().PostAsync(Path, Empty());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_WhenGateDisabled_Returns404()
    {
        // Default config has no Admin:BackfillEnabled → the endpoint stays hidden.
        var response = await factory.CreateAuthenticatedClient().PostAsync(Path, Empty());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Authenticated_WhenGateEnabled_Returns200WithCounts()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<BackfillChatMembershipsResult>>(), Arg.Any<CancellationToken>())
            .Returns(new BackfillChatMembershipsResult(2, 3));

        using var enabled = factory.WithWebHostBuilder(b => b.UseSetting("Admin:BackfillEnabled", "true"));
        var client = enabled.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.MintToken(Guid.NewGuid()));

        var response = await client.PostAsync(Path, Empty());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
