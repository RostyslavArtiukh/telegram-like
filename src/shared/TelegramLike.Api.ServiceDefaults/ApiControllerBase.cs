using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TelegramLike.Api.ServiceDefaults;

/// <summary>
/// Shared base for authenticated API controllers across every service. Resolves the acting user
/// from the validated JWT (<c>sub</c>, falling back to <see cref="ClaimTypes.NameIdentifier"/>)
/// once per request via an action filter: actions read <see cref="CurrentUserId"/> directly, and a
/// token with no parseable subject short-circuits with 401 before the action body runs — so no
/// action repeats the guard. Endpoints marked <c>[AllowAnonymous]</c> are skipped, so public
/// bootstrap endpoints (register / login) can share this base. Relies on
/// <c>MapInboundClaims = false</c> so the raw <c>sub</c> claim is preserved.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase, IActionFilter
{
    /// <summary>The authenticated user's id, resolved before the action runs.</summary>
    protected Guid CurrentUserId { get; private set; }

    void IActionFilter.OnActionExecuting(ActionExecutingContext context)
    {
        // Public endpoints ([AllowAnonymous]) carry no subject — let them through untouched.
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        var userId = ResolveUserId();
        if (userId is null)
            context.Result = Unauthorized();
        else
            CurrentUserId = userId.Value;
    }

    void IActionFilter.OnActionExecuted(ActionExecutedContext context) { }

    private Guid? ResolveUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
