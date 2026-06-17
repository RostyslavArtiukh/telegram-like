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
using TelegramLike.Chats.Api.Tests.Harness;

namespace TelegramLike.Chats.Api.Tests;

/// <summary>
/// DomainExceptionFilter contract for Chats:
///   InvalidOperationException → 400 ProblemDetails
///   ArgumentException         → 400 ProblemDetails
///   UnauthorizedAccessException → 403 ProblemDetails
///
/// Each test creates its own ChatsApiFactory to avoid NSubstitute When/Do
/// config accumulation across tests sharing the same mock instance.
/// The UnauthorizedAccessException case uses When/Do rather than .Throws() because
/// KickMemberCommand : IRequest (non-generic, i.e. IRequest&lt;Unit&gt;) and NSubstitute's
/// .Throws() does not reliably intercept that path through ISender.Send&lt;TResponse&gt;.
/// </summary>
public sealed class ChatsDomainExceptionFilterTests
{
    private static readonly Guid SomeChatId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── InvalidOperationException → 400 ProblemDetails ────────────────────

    [Fact]
    public async Task InvalidOperationException_Returns400WithProblemDetails()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("chat already exists"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new { name = "Dup" });
        var response = await client.PostAsync("/chats/group", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("chat already exists");
    }

    // ── ArgumentException → 400 ProblemDetails ────────────────────────────

    [Fact]
    public async Task ArgumentException_Returns400WithProblemDetails()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("name is invalid"));

        var client = factory.CreateAuthenticatedClient();
        var body = Json(new { name = "" });
        var response = await client.PostAsync("/chats/broadcast", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("name is invalid");
    }

    // ── UnauthorizedAccessException → 403 ProblemDetails ─────────────────

    [Fact]
    public async Task UnauthorizedAccessException_Returns403WithProblemDetails()
    {
        await using var factory = new ChatsApiFactory();
        // KickMemberCommand : IRequest (non-generic). Using When/Do on the concrete
        // type because .Throws() on Arg.Any<IRequest<Unit>>() does not reliably match
        // non-generic IRequest commands through ISender.Send<TResponse>.
        factory.Mediator
            .When(m => m.Send(Arg.Any<KickMemberCommand>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new UnauthorizedAccessException("only owners may kick"));

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

    // ── Response content-type is application/problem+json ─────────────────

    [Fact]
    public async Task ErrorResponse_HasProblemDetailsContentType()
    {
        await using var factory = new ChatsApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsync("/chats/group", Json(new { name = "x" }));

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
