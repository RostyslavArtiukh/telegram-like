using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramLike.Identity.Tests.Api.Harness;

namespace TelegramLike.Identity.Tests.Api;

/// <summary>
/// DomainExceptionFilter contract for Identity after [TL-98]:
///   <see cref="ValidationException"/> (FluentValidation) → 400 { error } with joined messages
///   <see cref="DomainException"/> (incl. the migrated value-object guards) → 400 { error }
///   framework exceptions → NOT mapped (bubble as a server error).
///
/// Identity keeps the legacy <c>{ "error": "..." }</c> body — not ProblemDetails — because
/// the Web BFF Identity client reads <c>error</c> off 400 responses.
///
/// Each test creates its own IdentityApiFactory to avoid NSubstitute config accumulation
/// across tests sharing the same mock instance.
/// </summary>
public sealed class IdentityDomainExceptionFilterTests
{
    private static StringContent RegisterBody() => new(
        JsonSerializer.Serialize(new
        {
            email = "user@example.com",
            username = "someuser",
            displayName = "Some User",
            password = "secret123",
            userId = Guid.NewGuid()
        }),
        Encoding.UTF8,
        "application/json");

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetString();
    }

    // ── DomainException (value-object guard) → 400 { error } ──────────────

    [Fact]
    public async Task DomainException_Returns400WithLegacyErrorBody()
    {
        await using var factory = new IdentityApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("Username cannot be empty."));

        var client = factory.CreateClient();
        var response = await client.PostAsync("/auth/register", RegisterBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("Username cannot be empty.");
    }

    // ── ValidationException → 400 { error } with joined messages ──────────

    [Fact]
    public async Task ValidationException_Returns400WithJoinedMessages()
    {
        await using var factory = new IdentityApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new ValidationException(new[]
            {
                new ValidationFailure("Email", "Email is required."),
                new ValidationFailure("Password", "Password is too short.")
            }));

        var client = factory.CreateClient();
        var response = await client.PostAsync("/auth/register", RegisterBody());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("Email is required. Password is too short.");
    }

    // ── framework exception is NOT mapped to 400 (bubbles as a server error) ──

    [Fact]
    public async Task FrameworkException_IsNotMappedTo400()
    {
        await using var factory = new IdentityApiFactory();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("sequence contains no elements"));

        var client = factory.CreateClient();

        HttpResponseMessage? response;
        try
        {
            response = await client.PostAsync("/auth/register", RegisterBody());
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
