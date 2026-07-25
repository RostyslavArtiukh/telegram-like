using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Client.Identity;

namespace TelegramLike.Web.Controllers;

/// <summary>
/// Auth callbacks the Blazor UI posts to via native <c>&lt;form method="post"&gt;</c>
/// (see <c>Login.razor</c> and <c>NavMenu.razor</c>). Not a REST/JSON API: these set or
/// clear the auth cookie and redirect. Kept as a controller — rather than inline in
/// <c>Program.cs</c> — to match the 5 services' convention that HTTP endpoints live in
/// <c>Controllers/</c> (see the <c>api_controllers</c> memory). Antiforgery is validated
/// manually (<see cref="IgnoreAntiforgeryTokenAttribute"/> suppresses the automatic check)
/// so a stale/forged post redirects cleanly instead of surfacing a raw 400.
/// </summary>
[Route("auth")]
public sealed class AuthController(IIdentityAuthApi identity, IAntiforgery antiforgery) : ControllerBase
{
    // Login: credentials -> session token -> identity claims -> cookie, all server-side in
    // this one request, so the session token is minted and consumed here and never reaches
    // the browser (no query string, no hidden field).
    [HttpPost("signin")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogIn([FromForm] string? email, [FromForm] string? password)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Redirect("/login?error=invalid");
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Redirect("/login?error=invalid");

        string sessionToken;
        try
        {
            sessionToken = await identity.LoginAsync(email, password);
        }
        catch (InvalidOperationException)
        {
            // Identity rejected the credentials.
            return Redirect("/login?error=invalid");
        }
        catch
        {
            // Downstream outage: timeout, open circuit breaker, connection failure, etc.
            return Redirect("/login?error=unavailable");
        }

        // Exchange the fresh session token at the IdP for the user's identity claims.
        var session = await identity.ExchangeAsync(sessionToken);
        if (session is null) return Redirect("/login?error=invalid");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.Username),
            new(ClaimTypes.Email, session.Email),
            new("session_token", sessionToken)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        await HttpContext.SignInAsync("Cookies", principal);

        return Redirect("/");
    }

    // Sign-out: a real <form> submit from NavMenu, guarded by the antiforgery token.
    [HttpPost("signout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogOut()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest("Invalid request.");
        }

        // Revoke the session server-side (delete it from Redis at the IdP) so the opaque
        // token can't mint further access JWTs after logout — not just drop the cookie.
        // Best-effort by the client's contract: a downstream failure won't block sign-out.
        var sessionToken = HttpContext.User.FindFirst("session_token")?.Value;
        if (!string.IsNullOrEmpty(sessionToken))
            await identity.LogoutAsync(sessionToken);

        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/login");
    }
}
