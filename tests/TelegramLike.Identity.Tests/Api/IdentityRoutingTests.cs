using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Identity.Application.Queries.GetUserById;
using TelegramLike.Identity.Tests.Api.Harness;

namespace TelegramLike.Identity.Tests.Api;

/// <summary>
/// Routing contract for the authenticated <c>/users</c> lookups: every route + verb resolves
/// to its action and translates the handler result to the right status. Wrong verb on a known
/// path → 405; unknown path → 404; a non-guid <c>{id}</c> segment fails the route constraint → 404.
/// The mediator is mocked, so these assert the Api layer (routing, binding, result mapping) only.
/// </summary>
public sealed class IdentityRoutingTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private static readonly Guid SomeUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    private static UserDto SampleUser(Guid id) =>
        new(id, "user@example.com", "someuser", "Some User", null, false, DateTime.UtcNow);

    // ── unknown path / wrong verb ──────────────────────────────────────────

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var response = await Auth().GetAsync("/users/does-not-exist-route/extra");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostOnGetByIdPath_Returns405()
    {
        var response = await Auth().PostAsync($"/users/{SomeUserId}", new StringContent(""));
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetOnByIdsPath_Returns405()
    {
        // /users/by-ids is POST-only; a GET on the same template is method-not-allowed.
        var response = await Auth().GetAsync("/users/by-ids");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NonGuidUserId_Returns404()
    {
        // {id:guid} constraint rejects a non-guid segment before the action is reached.
        var response = await Auth().GetAsync("/users/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /users/{id} → 200 when found, 404 when null ───────────────────

    [Fact]
    public async Task GetById_WhenFound_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<UserDto?>>(), Arg.Any<CancellationToken>())
            .Returns(SampleUser(SomeUserId));

        var response = await Auth().GetAsync($"/users/{SomeUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<UserDto?>>(), Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);

        var response = await Auth().GetAsync($"/users/{SomeUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /users/by-ids → 200 with the username map ────────────────────

    [Fact]
    public async Task GetUsernamesByIds_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<IReadOnlyDictionary<Guid, string>>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [SomeUserId] = "someuser" });

        var body = new StringContent(
            JsonSerializer.Serialize(new[] { SomeUserId }), Encoding.UTF8, "application/json");
        var response = await Auth().PostAsync("/users/by-ids", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /users/by-username → 200 when resolved, 404 when null ─────────

    [Fact]
    public async Task GetIdByUsername_WhenResolved_Returns200()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid?>>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)SomeUserId);

        var response = await Auth().GetAsync("/users/by-username?u=someuser");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetIdByUsername_WhenUnresolved_Returns404()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid?>>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var response = await Auth().GetAsync("/users/by-username?u=ghost");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
