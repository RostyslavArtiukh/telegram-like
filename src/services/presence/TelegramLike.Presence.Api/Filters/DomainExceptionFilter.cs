using Microsoft.AspNetCore.Mvc.Filters;

namespace TelegramLike.Presence.Api.Filters;

/// <summary>
/// Global exception filter for the presence API. Registered (see <c>Program.cs</c>) to mirror
/// the structure of the Chats/Identity/Notifications services, but kept deliberately empty.
/// <para>
/// The former minimal API wrapped <b>no</b> endpoint in a try/catch and called no
/// <c>Results.Problem(...)</c>: every handler exception bubbled up to ASP.NET's default
/// handler and surfaced as a raw <c>500</c>. The only exception the command handlers can throw
/// is <see cref="ArgumentException"/> (empty <c>UserId</c>), which is unreachable on the wire
/// because the id always comes from the validated JWT <c>sub</c>. So Presence has never emitted
/// a <c>400</c>/<c>403</c> <c>ProblemDetails</c> body from any endpoint.
/// </para>
/// <para>
/// Reproducing that contract means mapping nothing: we leave every exception unhandled so it
/// still bubbles up as a <c>500</c>, exactly as before. Mapping <see cref="InvalidOperationException"/>
/// (→400, like Chats) or <see cref="ArgumentException"/> would silently change the wire contract
/// the Web BFF Presence client reads, so we intentionally do not — the same reasoning the
/// Notifications filter documents for the codes it omits.
/// </para>
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // No-op: presence emits no domain-exception → ProblemDetails mapping. Unhandled
        // exceptions bubble up as a 500, matching the previous minimal-API behaviour.
    }
}
