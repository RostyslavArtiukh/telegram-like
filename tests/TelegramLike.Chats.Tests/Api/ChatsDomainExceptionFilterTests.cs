using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Tests.Api.Harness;
using TelegramLike.Chats.Domain;

namespace TelegramLike.Chats.Tests.Api;

/// <summary>
/// DomainExceptionFilter contract for Chats after the domain-exception refactor:
///   <see cref="DomainException"/>    → 400 ProblemDetails
///   <see cref="ForbiddenException"/> → 403 ProblemDetails
///   framework exceptions (raw InvalidOperationException / ArgumentException) → 500, NOT 400.
///
/// The last case is the deliberate behaviour change: the previous filter caught the raw BCL
/// base types, so a framework-thrown exception (LINQ, the Mongo driver, a data-integrity default
/// case) was mislabelled as a client 400 with an internal message. Now only deliberate domain
/// exceptions map to 4xx.
///
/// Each test creates its own ChatsApiFactory to avoid NSubstitute When/Do config accumulation
/// across tests sharing the same mock instance.
/// </summary>
public sealed class ChatsDomainExceptionFilterTests
{
    private static readonly Guid SomeChatId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── DomainException → 400 ProblemDetails ──────────────────────────────

    [Fact]
    public async Task DomainException_Returns400WithProblemDetails()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("chat already exists"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new { name = "Dup" });
        var response = await client.PostAsync("/chats/group", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("chat already exists");
    }

    // ── ForbiddenException → 403 ProblemDetails ───────────────────────────

    [Fact]
    public async Task ForbiddenException_Returns403WithProblemDetails()
    {
        await using var factory = new ChatsApiFactory();
        // KickMemberCommand : IRequest (non-generic). Using When/Do on the concrete
        // type because .Throws() on Arg.Any<IRequest<Unit>>() does not reliably match
        // non-generic IRequest commands through ISender.Send<TResponse>.
        factory.Mediator
            .When(m => m.Send(Arg.Any<KickMemberCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ForbiddenException("only owners may kick"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync(
            $"/chats/{SomeChatId}/members/{Guid.NewGuid()}/kick",
            Json(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(403);
        problem.Detail.Should().Be("only owners may kick");
    }

    // ── framework exception is NOT mapped to 400 (bubbles as a server error) ──

    [Fact]
    public async Task FrameworkException_IsNotMappedTo400()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("sequence contains no elements"));

        var client = factory.CreateAuthenticatedClient();

        HttpResponseMessage? response;
        try
        {
            response = await client.PostAsync("/chats/group", Json(new { name = "x" }));
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
    public async Task ErrorResponse_HasProblemDetailsContentType()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("boom"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/chats/group", Json(new { name = "x" }));

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
