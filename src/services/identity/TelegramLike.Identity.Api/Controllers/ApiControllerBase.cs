using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TelegramLike.Identity.Api.Controllers;

/// <summary>
/// Shared base for authenticated API controllers. Resolves the acting user from the
/// validated JWT (<c>sub</c>, falling back to <see cref="ClaimTypes.NameIdentifier"/>).
/// Relies on <c>MapInboundClaims = false</c> so the raw <c>sub</c> claim is preserved.
/// Designed to be copied verbatim into the other services.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// The authenticated user's id, or <see cref="Guid.Empty"/> if the JWT carries no usable subject.
    /// Prefer <see cref="TryGetUserId"/> when an explicit 401 is required.
    /// </summary>
    protected Guid CurrentUserId => TryGetUserId(out var userId) ? userId : Guid.Empty;

    /// <summary>
    /// Attempts to resolve the acting user id from the JWT <c>sub</c> claim, falling back to
    /// <see cref="ClaimTypes.NameIdentifier"/>. Returns <c>false</c> when no parseable id is present.
    /// </summary>
    protected bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId);
    }
}
