using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramLike.Messaging.Tests.Api.Harness;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Domain;

namespace TelegramLike.Messaging.Tests.Api;

/// <summary>
/// DomainExceptionFilter contract for Messaging after the domain-exception refactor:
///   <see cref="DomainException"/>    → 400 ProblemDetails
///   <see cref="ForbiddenException"/> → 403 ProblemDetails
///   framework exceptions (raw InvalidOperationException / ArgumentException) → 500, NOT 400.
///
/// (FluentValidation's ValidationException also maps to 400, exercised elsewhere.) The framework
/// case is the deliberate behaviour change: the previous filter caught the raw BCL base types, so
/// a framework-thrown exception was mislabelled as a client 400 with an internal message.
///
/// Each test creates its own MessagingApiFactory to avoid NSubstitute When/Do config
/// accumulation across tests sharing the same mock instance.
/// </summary>
public sealed class MessagingDomainExceptionFilterTests
{
    private static readonly Guid SomeMessageId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SomeChatId    = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SomeUserId    = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── DomainException → 400 ProblemDetails ──────────────────────────────

    [Fact]
    public async Task DomainException_returns_400_with_ProblemDetails()
    {
        await using var factory = new MessagingApiFactory();
        // POST /messages → SendMessageCommand : IRequest<Guid> — .Throws() on IRequest<Guid> works.
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("message text required"));

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

    // ── ForbiddenException → 403 ProblemDetails ───────────────────────────

    [Fact]
    public async Task ForbiddenException_returns_403_with_ProblemDetails()
    {
        await using var factory = new MessagingApiFactory();
        // POST /messages/{id}/retract → RetractMessageCommand : IRequest (non-generic).
        // When/Do is required because .Throws() on Arg.Any<IRequest<Unit>>() does not
        // reliably intercept non-generic IRequest calls through ISender.Send<TResponse>.
        factory.Mediator
            .When(m => m.Send(Arg.Any<RetractMessageCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ForbiddenException("not the author"));

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

    // ── framework exception is NOT mapped to 400 (bubbles as a server error) ──

    [Fact]
    public async Task Framework_exception_is_not_mapped_to_400()
    {
        await using var factory = new MessagingApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("sequence contains no elements"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new
        {
            chatId = SomeChatId,
            text = "x",
            recipients = new[] { SomeUserId },
            isBroadcast = false
        });

        HttpResponseMessage? response;
        try
        {
            response = await client.PostAsync("/messages", body);
        }
        catch (InvalidOperationException)
        {
            // TestServer rethrew the unhandled exception — that alone proves the filter
            // no longer converts a framework exception into a client 400.
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── Response content-type is application/problem+json ─────────────────

    [Fact]
    public async Task Error_response_has_ProblemDetails_content_type()
    {
        await using var factory = new MessagingApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("boom"));

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
