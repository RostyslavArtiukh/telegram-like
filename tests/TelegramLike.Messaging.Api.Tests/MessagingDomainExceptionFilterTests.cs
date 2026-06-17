using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramLike.Messaging.Api.Tests.Harness;
using TelegramLike.Messaging.Application.Commands.RetractMessage;

namespace TelegramLike.Messaging.Api.Tests;

/// <summary>
/// DomainExceptionFilter contract for Messaging:
///   InvalidOperationException → 400 ProblemDetails
///   ArgumentException         → 400 ProblemDetails
///   UnauthorizedAccessException → 403 ProblemDetails
///
/// Each test creates its own MessagingApiFactory to avoid NSubstitute When/Do
/// config accumulation across tests sharing the same mock instance.
/// </summary>
public sealed class MessagingDomainExceptionFilterTests
{
    private static readonly Guid SomeMessageId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SomeChatId    = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SomeUserId    = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── InvalidOperationException → 400 ProblemDetails ────────────────────

    [Fact]
    public async Task InvalidOperationException_Returns400WithProblemDetails()
    {
        await using var factory = new MessagingApiFactory();
        // POST /messages → SendMessageCommand : IRequest<Guid> — .Throws() on IRequest<Guid> works.
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("message text required"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new
        {
            chatId = SomeChatId,
            text = (string?)null,
            recipients = new[] { SomeUserId },
            isBroadcast = false
        });
        var response = await client.PostAsync("/messages", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("message text required");
    }

    // ── ArgumentException → 400 ProblemDetails ────────────────────────────

    [Fact]
    public async Task ArgumentException_Returns400WithProblemDetails()
    {
        await using var factory = new MessagingApiFactory();
        // POST /messages/{id}/retract → RetractMessageCommand : IRequest (non-generic).
        // When/Do is required because .Throws() on Arg.Any<IRequest<Unit>>() does not
        // reliably intercept non-generic IRequest calls through ISender.Send<TResponse>.
        factory.Mediator
            .When(m => m.Send(Arg.Any<RetractMessageCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ArgumentException("invalid retract target"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync(
            $"/messages/{SomeMessageId}/retract",
            Json(new { actorIsModerator = false }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("invalid retract target");
    }

    // ── UnauthorizedAccessException → 403 ProblemDetails ─────────────────

    [Fact]
    public async Task UnauthorizedAccessException_Returns403WithProblemDetails()
    {
        await using var factory = new MessagingApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<RetractMessageCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new UnauthorizedAccessException("not the author"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync(
            $"/messages/{SomeMessageId}/retract",
            Json(new { actorIsModerator = false }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(403);
        problem.Detail.Should().Be("not the author");
    }

    // ── Response content-type is application/problem+json ─────────────────

    [Fact]
    public async Task ErrorResponse_HasProblemDetailsContentType()
    {
        await using var factory = new MessagingApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new
        {
            chatId = SomeChatId,
            text = "x",
            recipients = new[] { SomeUserId },
            isBroadcast = false
        });
        var response = await client.PostAsync("/messages", body);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
