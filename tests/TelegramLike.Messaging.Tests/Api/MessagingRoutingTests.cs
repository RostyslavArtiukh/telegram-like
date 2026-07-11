using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Messaging.Tests.Api.Harness;
using TelegramLike.Messaging.Application.Queries;

namespace TelegramLike.Messaging.Tests.Api;

/// <summary>
/// Routing contract: every route + verb resolves to its action.
/// Wrong verb on a known path → 405. Unknown path → 404. Non-guid segment → 404.
/// </summary>
public sealed class MessagingRoutingTests(MessagingApiFactory factory) : IClassFixture<MessagingApiFactory>
{
    private static readonly Guid SomeMessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SomeChatId    = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SomeUserId    = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static MessageDto MakeMessageDto(Guid msgId, Guid chatId) =>
        new(msgId, chatId, SomeUserId, "hi",
            Array.Empty<AttachmentDto>(), null, null, null,
            Array.Empty<ReactionDto>(), false, null, null, null,
            DateTime.UtcNow);

    // ── 404 for truly unknown paths ────────────────────────────────────────

    [Fact]
    public async Task Unknown_path_returns_404()
    {
        var response = await Auth().GetAsync("/messages/unknown-route-segment");
        // "unknown-route-segment" fails the :guid constraint → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 405 for wrong verb on known paths ──────────────────────────────────

    [Fact]
    public async Task DELETE_on_the_POST_only_messages_route_returns_405()
    {
        var response = await Auth().DeleteAsync("/messages");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task POST_on_the_GET_only_message_route_returns_405()
    {
        // GET /messages/{id} exists; POST on that same path is not defined
        // The framework should return 405.
        var response = await Auth().PostAsync($"/messages/{SomeMessageId}", Json(new { }));
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // ── non-guid route segment → 404 (constraint rejects) ─────────────────

    [Fact]
    public async Task Non_guid_message_id_returns_404()
    {
        var response = await Auth().GetAsync("/messages/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Non_guid_chat_id_returns_404()
    {
        var response = await Auth().GetAsync("/chats/not-a-guid/messages");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /messages → 201 Created ──────────────────────────────────────

    [Fact]
    public async Task SendMessage_returns_201_Created()
    {
        var newId = Guid.NewGuid();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var body = Json(new
        {
            chatId = SomeChatId,
            text = "hello",
            recipients = new[] { SomeUserId },
            isBroadcast = false
        });
        var response = await Auth().PostAsync("/messages", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/messages/{newId}");
    }

    // ── GET /messages/{id} → 200 when found, 404 when null ────────────────

    [Fact]
    public async Task GetMessageById_when_found_returns_200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<MessageDto?>>(), Arg.Any<CancellationToken>())
            .Returns(MakeMessageDto(SomeMessageId, SomeChatId));

        var response = await Auth().GetAsync($"/messages/{SomeMessageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMessageById_when_not_found_returns_404()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<MessageDto?>>(), Arg.Any<CancellationToken>())
            .Returns((MessageDto?)null);

        var response = await Auth().GetAsync($"/messages/{SomeMessageId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /messages/{id}/retract → 204 ─────────────────────────────────

    [Fact]
    public async Task Retract_returns_204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync(
            $"/messages/{SomeMessageId}/retract",
            Json(new { actorIsModerator = false }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /messages/{id}/hide → 204 ────────────────────────────────────

    [Fact]
    public async Task Hide_returns_204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync(
            $"/messages/{SomeMessageId}/hide",
            Json(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /chats/{id}/messages → 200 ────────────────────────────────────

    [Fact]
    public async Task GetChatMessages_returns_200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<MessagePageDto>>(), Arg.Any<CancellationToken>())
            .Returns(new MessagePageDto(Array.Empty<MessageDto>(), null));

        var response = await Auth().GetAsync($"/chats/{SomeChatId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /messages/{id}/reactions → 204 ───────────────────────────────

    [Fact]
    public async Task AddReaction_returns_204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = Json(new { emoji = "Like", actorIsPremium = false });
        var response = await Auth().PostAsync($"/messages/{SomeMessageId}/reactions", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── DELETE /messages/{id}/reactions/{emoji} → 204 for valid emoji ─────

    [Fact]
    public async Task RemoveReaction_valid_emoji_returns_204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().DeleteAsync($"/messages/{SomeMessageId}/reactions/Like");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── DELETE /messages/{id}/reactions/{emoji} → 400 for unknown emoji ───

    [Fact]
    public async Task RemoveReaction_unknown_emoji_returns_400()
    {
        var response = await Auth().DeleteAsync(
            $"/messages/{SomeMessageId}/reactions/NotARealEmoji");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /messages/{id}/read → 204 ────────────────────────────────────

    [Fact]
    public async Task MarkAsRead_returns_204()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var response = await Auth().PostAsync(
            $"/messages/{SomeMessageId}/read",
            Json(new { isBroadcast = false }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
