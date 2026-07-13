using System.Net;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Notifications.Application.Queries;
using TelegramLike.Notifications.Tests.Api.Harness;

namespace TelegramLike.Notifications.Tests.Api;

/// <summary>
/// Routing contract for the notifications endpoints: every route + verb resolves to its action.
/// Wrong verb on a known path → 405; unknown path → 404; a non-guid <c>{id}</c> fails the route
/// constraint → 404. The mediator is mocked (non-generic <c>IRequest</c> mark-commands need no
/// setup — the substitute completes them), so these assert the Api layer only.
/// </summary>
public sealed class NotificationsRoutingTests(NotificationsApiFactory factory) : IClassFixture<NotificationsApiFactory>
{
    private static readonly Guid SomeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    private static StringContent Empty() => new("{}", System.Text.Encoding.UTF8, "application/json");

    // ── unknown path / wrong verb / bad constraint ────────────────────────

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var response = await Auth().GetAsync("/notifications/does-not-exist/extra");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostOnFeedPath_Returns405()
    {
        // GET /notifications (feed) exists; POST on the same template is method-not-allowed.
        var response = await Auth().PostAsync("/notifications", Empty());
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task DeleteOnUnreadCount_Returns405()
    {
        var response = await Auth().DeleteAsync("/notifications/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NonGuidNotificationId_Returns404()
    {
        var response = await Auth().PostAsync("/notifications/not-a-guid/read", Empty());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /notifications (feed) → 200 ───────────────────────────────────

    [Fact]
    public async Task GetFeed_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<NotificationFeedDto>>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationFeedDto(Array.Empty<NotificationDto>(), null));

        var response = await Auth().GetAsync("/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /notifications/unread-count → 200 ─────────────────────────────

    [Fact]
    public async Task GetUnreadCount_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<long>>(), Arg.Any<CancellationToken>())
            .Returns(3L);

        var response = await Auth().GetAsync("/notifications/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST mark-read endpoints → 204 NoContent ──────────────────────────

    [Fact]
    public async Task MarkAsRead_Returns204()
    {
        var response = await Auth().PostAsync($"/notifications/{SomeId}/read", Empty());
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkAllAsRead_Returns204()
    {
        var response = await Auth().PostAsync("/notifications/read-all", Empty());
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkChatAsRead_Returns204()
    {
        var response = await Auth().PostAsync($"/notifications/chats/{SomeId}/read", Empty());
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
