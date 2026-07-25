using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Identity.Api.Contracts;
using TelegramLike.Identity.Application.Auth.ExchangeSession;
using TelegramLike.Identity.Application.Commands.EndSession;
using TelegramLike.Identity.Application.Commands.LoginUser;
using TelegramLike.Identity.Application.Commands.RegisterUser;

namespace TelegramLike.Identity.Api.Controllers;

/// <summary>
/// Public auth endpoints — no bearer required because the caller isn't authenticated yet.
/// These bootstrap authentication (register / login → session token / session token → access JWT).
/// Validation and business failures throw and are mapped to 400 { error } by the global
/// <see cref="Filters.DomainExceptionFilter"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new RegisterUserCommand(body.Email, body.Username, body.DisplayName, body.Password, body.UserId), cancellationToken);
        return Ok(new { userId = id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken cancellationToken)
    {
        var token = await mediator.Send(new LoginUserCommand(body.Email, body.Password), cancellationToken);
        return Ok(new { sessionToken = token });
    }

    // Exchange an opaque session token for a short-lived access JWT + identity claims.
    // Possession of a valid session token is the credential, so this stays public.
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest body, CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new ExchangeSessionQuery(body.SessionToken), cancellationToken);
        return dto is null ? Unauthorized() : Ok(dto);
    }

    // Logout: revoke the session token so it stops minting access JWTs. Possession of the
    // token is the credential (same as /token), so this stays public. Idempotent — an
    // unknown/expired token is a no-op → always 204, so a client can call it best-effort.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new EndSessionCommand(body.SessionToken), cancellationToken);
        return NoContent();
    }
}
