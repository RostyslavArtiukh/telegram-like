using System.Net;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Presence.Application.Queries;
using TelegramLike.Presence.Domain.ValueObjects;
using TelegramLike.Presence.Tests.Api.Harness;

namespace TelegramLike.Presence.Tests.Api;

/// <summary>
/// Routing contract for the presence + typing endpoints: every route + verb resolves to its
/// action. Wrong verb on a known path → 405; unknown path → 404; a non-guid <c>{id}</c> fails
/// the route constraint → 404. The mediator is mocked (non-generic <c>IRequest</c> transition
/// commands need no setup — the substitute completes them), so these assert the Api layer only.
/// </summary>
public sealed class PresenceRoutingTests(PresenceApiFactory factory) : IClassFixture<PresenceApiFactory>
{
    private static readonly Guid SomeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    private static StringContent Empty() => new("[]", System.Text.Encoding.UTF8, "application/json");

    // ── unknown path / wrong verb / bad constraint ────────────────────────

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var response = await Auth().GetAsync("/presence/does-not-exist/extra/more");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOnHeartbeat_Returns405()
    {
        // POST /presence/heartbeat exists; GET on the same template is method-not-allowed.
        var response = await Auth().GetAsync("/presence/heartbeat");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NonGuidUserId_Returns404()
    {
        var response = await Auth().GetAsync("/presence/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST transition commands → 204 NoContent ──────────────────────────

    [Fact]
    public async Task Heartbeat_Returns204()
    {
        var response = await Auth().PostAsync("/presence/heartbeat", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GoOffline_Returns204()
    {
        var response = await Auth().PostAsync("/presence/offline", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StartTyping_Returns204()
    {
        var response = await Auth().PostAsync($"/presence/typing/{SomeId}/start", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StopTyping_Returns204()
    {
        var response = await Auth().PostAsync($"/presence/typing/{SomeId}/stop", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /presence/{id} → 200 when found, 404 when null ────────────────

    [Fact]
    public async Task GetUserPresence_WhenFound_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<UserPresenceDto?>>(), Arg.Any<CancellationToken>())
            .Returns(new UserPresenceDto(SomeId, OnlineStatus.Online, DateTime.UtcNow, false));

        var response = await Auth().GetAsync($"/presence/{SomeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserPresence_WhenNotFound_Returns404()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<UserPresenceDto?>>(), Arg.Any<CancellationToken>())
            .Returns((UserPresenceDto?)null);

        var response = await Auth().GetAsync($"/presence/{SomeId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /presence/batch → 200 with the presence map ──────────────────

    [Fact]
    public async Task GetBatchPresence_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<IReadOnlyDictionary<Guid, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [SomeId] = true });

        var response = await Auth().PostAsync("/presence/batch", Empty());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /presence/typing/{id} → 200 ───────────────────────────────────

    [Fact]
    public async Task GetTypingUsers_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<TypingUsersDto>>(), Arg.Any<CancellationToken>())
            .Returns(new TypingUsersDto(SomeId, Array.Empty<Guid>()));

        var response = await Auth().GetAsync($"/presence/typing/{SomeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
