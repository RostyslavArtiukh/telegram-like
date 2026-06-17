using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Chats.Api.Tests.Harness;
using TelegramLike.Chats.Application.Queries;

namespace TelegramLike.Chats.Api.Tests;

/// <summary>
/// Routing contract: every route + verb resolves to its action.
/// Wrong verb on a known path → 405. Unknown path → 404. Non-guid segment → 404.
/// </summary>
public sealed class ChatsRoutingTests(ChatsApiFactory factory) : IClassFixture<ChatsApiFactory>
{
    private static readonly Guid SomeChatId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SomeUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ── helpers ────────────────────────────────────────────────────────────────

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── 404 for unknown paths ───────────────────────────────────────────────

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var response = await Auth().GetAsync("/chats/does-not-exist-route");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 405 for wrong verb on known paths ──────────────────────────────────

    [Fact]
    public async Task GetOnPostDirect_Returns405()
    {
        var response = await Auth().GetAsync("/chats/direct");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task DeleteOnGetMy_Returns405()
    {
        var response = await Auth().DeleteAsync("/chats/my");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ── non-guid route segment → 404 (constraint rejects) ─────────────────

    [Fact]
    public async Task NonGuidChatId_Returns404()
    {
        var response = await Auth().GetAsync("/chats/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonGuidChatIdForMembers_Returns404()
    {
        var response = await Auth().GetAsync("/chats/not-a-guid/members");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /chats/my → 200 OK ─────────────────────────────────────────────

    [Fact]
    public async Task GetMyChats_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<IReadOnlyList<ChatSummaryDto>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ChatSummaryDto>());

        var response = await Auth().GetAsync("/chats/my");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /chats/{id} → 200 when found, 404 when null ───────────────────

    [Fact]
    public async Task GetChatById_WhenFound_Returns200()
    {
        var dto = new TelegramLike.Chats.Application.Queries.ChatDetailsDto(
            SomeChatId,
            TelegramLike.Chats.Domain.ValueObjects.ChatType.Group,
            "Test Chat",
            SomeUserId,
            DateTime.UtcNow,
            false,
            Array.Empty<ChatMemberDto>());

        factory.Mediator
            .Send(Arg.Any<IRequest<TelegramLike.Chats.Application.Queries.ChatDetailsDto?>>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var response = await Auth().GetAsync($"/chats/{SomeChatId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChatById_WhenNotFound_Returns404()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<TelegramLike.Chats.Application.Queries.ChatDetailsDto?>>(), Arg.Any<CancellationToken>())
            .Returns((TelegramLike.Chats.Application.Queries.ChatDetailsDto?)null);

        var response = await Auth().GetAsync($"/chats/{SomeChatId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /chats/{id}/members → 200 ─────────────────────────────────────

    [Fact]
    public async Task GetChatMembers_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<IReadOnlyList<ChatMemberDto>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ChatMemberDto>());

        var response = await Auth().GetAsync($"/chats/{SomeChatId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /chats/direct → 201 Created ──────────────────────────────────

    [Fact]
    public async Task CreateDirect_Returns201Created()
    {
        var newId = Guid.NewGuid();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var body = Json(new { peerUserId = SomeUserId });
        var response = await Auth().PostAsync("/chats/direct", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/chats/{newId}");
    }

    // ── POST /chats/group → 201 Created ───────────────────────────────────

    [Fact]
    public async Task CreateGroup_Returns201Created()
    {
        var newId = Guid.NewGuid();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var body = Json(new { name = "My Group" });
        var response = await Auth().PostAsync("/chats/group", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/chats/{newId}");
    }

    // ── POST /chats/broadcast → 201 Created ───────────────────────────────

    [Fact]
    public async Task CreateBroadcast_Returns201Created()
    {
        var newId = Guid.NewGuid();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var body = Json(new { name = "My Channel" });
        var response = await Auth().PostAsync("/chats/broadcast", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/chats/{newId}");
    }

    // ── PATCH /chats/{id} → 204 NoContent ─────────────────────────────────

    [Fact]
    public async Task RenameChat_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/chats/{SomeChatId}")
        {
            Content = Json(new { newName = "Renamed" })
        };
        // Need auth header on this manually-constructed request
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.MintToken(SomeUserId));
        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /chats/{id}/join → 204 NoContent ─────────────────────────────

    [Fact]
    public async Task JoinChat_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync($"/chats/{SomeChatId}/join", Json(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /chats/{id}/leave → 204 NoContent ────────────────────────────

    [Fact]
    public async Task LeaveChat_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync($"/chats/{SomeChatId}/leave", Json(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /chats/{chatId}/members/{targetUserId}/kick → 204 ────────────

    [Fact]
    public async Task KickMember_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync(
            $"/chats/{SomeChatId}/members/{SomeUserId}/kick", Json(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /chats/{chatId}/members/{targetUserId}/role → 204 ────────────

    [Fact]
    public async Task ChangeMemberRole_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = Json(new { newRole = "Admin" });
        var response = await Auth().PostAsync(
            $"/chats/{SomeChatId}/members/{SomeUserId}/role", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /chats/{id}/transfer-ownership → 204 ─────────────────────────

    [Fact]
    public async Task TransferOwnership_Returns204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = Json(new { newOwnerUserId = SomeUserId });
        var response = await Auth().PostAsync($"/chats/{SomeChatId}/transfer-ownership", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
