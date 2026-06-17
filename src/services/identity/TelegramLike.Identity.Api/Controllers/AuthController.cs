using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Identity.Api.Contracts;
using TelegramLike.Identity.Application.Auth.ExchangeSession;
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
public sealed class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new RegisterUserCommand(body.Email, body.Username, body.DisplayName, body.Password), ct);
        return Ok(new { userId = id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var token = await _mediator.Send(new LoginUserCommand(body.Email, body.Password), ct);
        return Ok(new { sessionToken = token });
    }

    // Exchange an opaque session token for a short-lived access JWT + identity claims.
    // Possession of a valid session token is the credential, so this stays public.
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest body, CancellationToken ct)
    {
        var dto = await _mediator.Send(new ExchangeSessionQuery(body.SessionToken), ct);
        return dto is null ? Unauthorized() : Ok(dto);
    }
}
