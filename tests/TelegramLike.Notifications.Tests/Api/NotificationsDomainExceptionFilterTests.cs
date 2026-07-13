using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;
using TelegramLike.Notifications.Tests.Api.Harness;

namespace TelegramLike.Notifications.Tests.Api;

/// <summary>
/// DomainExceptionFilter contract for Notifications after [TL-98]:
///   <see cref="DomainException"/> (incl. the migrated empty-id guards) → 400 ProblemDetails
///   framework exceptions (raw InvalidOperationException / ArgumentException) → 500, NOT 400.
///
/// Each test creates its own NotificationsApiFactory to avoid NSubstitute When/Do config
/// accumulation across tests sharing the same mock instance. MarkAllNotificationsAsReadCommand
/// is a non-generic IRequest, so throws are configured via When/Do on the concrete type
/// (.Throws() on Arg.Any&lt;IRequest&gt; does not reliably match through ISender.Send).
/// </summary>
public sealed class NotificationsDomainExceptionFilterTests
{
    // ── DomainException → 400 ProblemDetails ──────────────────────────────

    [Fact]
    public async Task DomainException_Returns400WithProblemDetails()
    {
        await using var factory = new NotificationsApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<MarkAllNotificationsAsReadCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DomainException("RecipientId cannot be empty."));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/notifications/read-all", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("RecipientId cannot be empty.");
    }

    // ── Response content-type is application/problem+json ─────────────────

    [Fact]
    public async Task ErrorResponse_HasProblemDetailsContentType()
    {
        await using var factory = new NotificationsApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<MarkAllNotificationsAsReadCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DomainException("boom"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/notifications/read-all", content: null);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    // ── framework exception is NOT mapped to 400 (bubbles as a server error) ──

    [Fact]
    public async Task FrameworkException_IsNotMappedTo400()
    {
        await using var factory = new NotificationsApiFactory();
        factory.Mediator
            .When(m => m.Send(Arg.Any<MarkAllNotificationsAsReadCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("sequence contains no elements"));

        var client = factory.CreateAuthenticatedClient();

        HttpResponseMessage? response;
        try
        {
            response = await client.PostAsync("/notifications/read-all", content: null);
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
