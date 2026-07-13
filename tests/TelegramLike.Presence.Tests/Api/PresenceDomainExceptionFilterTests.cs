using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using TelegramLike.Presence.Application.Commands.GoOffline;
using TelegramLike.Presence.Application.Commands.Heartbeat;
using TelegramLike.Presence.Tests.Api.Harness;

namespace TelegramLike.Presence.Tests.Api;

/// <summary>
/// DomainExceptionFilter contract for Presence after [TL-98] (before which the filter was a
/// deliberate no-op and every handler exception surfaced as a raw 500):
///   <see cref="DomainException"/>    → 400 ProblemDetails
///   <see cref="ForbiddenException"/> → 403 ProblemDetails
///   framework exceptions → 500, NOT 400.
///
/// Each test creates its own PresenceApiFactory to avoid NSubstitute When/Do config
/// accumulation. Heartbeat/GoOffline commands are non-generic IRequests, so throws are
/// configured via When/Do on the concrete type.
/// </summary>
public sealed class PresenceDomainExceptionFilterTests
{
    // ── DomainException → 400 ProblemDetails ──────────────────────────────

    [Fact]
    public async Task DomainException_Returns400WithProblemDetails()
    {
        await using var factory = new PresenceApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<HeartbeatCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DomainException("UserId cannot be empty."));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/presence/heartbeat", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("UserId cannot be empty.");
    }

    // ── ForbiddenException → 403 ProblemDetails ───────────────────────────

    [Fact]
    public async Task ForbiddenException_Returns403WithProblemDetails()
    {
        await using var factory = new PresenceApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<GoOfflineCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ForbiddenException("not your presence"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/presence/offline", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(403);
        problem.Detail.Should().Be("not your presence");
    }

    // ── framework exception is NOT mapped to 400 (bubbles as a server error) ──

    [Fact]
    public async Task FrameworkException_IsNotMappedTo400()
    {
        await using var factory = new PresenceApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<HeartbeatCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("sequence contains no elements"));

        var client = factory.CreateAuthenticatedClient();

        HttpResponseMessage? response;
        try
        {
            response = await client.PostAsync("/presence/heartbeat", content: null);
        }
        catch (InvalidOperationException)
        {
            // TestServer rethrew the unhandled exception — that alone proves the filter
            // does not convert a framework exception into a client 400.
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
