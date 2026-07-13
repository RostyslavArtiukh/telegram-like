using TelegramLike.Domain.ServiceDefaults;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace TelegramLike.Presence.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses. Registered globally (see <c>Program.cs</c>).
/// <para>
/// Mapping ([TL-98]):
/// <list type="bullet">
///   <item><see cref="ForbiddenException"/> → 403</item>
///   <item><see cref="DomainException"/> → 400</item>
/// </list>
/// Until [TL-98] this filter was a deliberate no-op: the original minimal API had no error
/// handling, so every handler exception surfaced as a raw 500 and the only throwable guards
/// were unreachable from the wire (the user id always comes from the validated JWT <c>sub</c>).
/// Those guards now throw <see cref="DomainException"/>, so mapping them costs nothing and
/// makes presence consistent with the other services. Framework-thrown exceptions (LINQ, the
/// Mongo/Redis drivers) stay unmapped and bubble up as a 500 — an internal failure is never
/// mislabelled as a client error.
/// </para>
/// <remarks>
/// Mapped exceptions are logged with the current trace id (also echoed in the <c>traceId</c>
/// ProblemDetails extension) so a client-reported error correlates with its Jaeger trace —
/// the same shape the Notifications filter emits.
/// </remarks>
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            ForbiddenException => StatusCodes.Status403Forbidden,
            DomainException => StatusCodes.Status400BadRequest,
            _ => (int?)null
        };

        if (statusCode is null)
            return;

        var traceId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        logger.LogWarning(
            "Domain exception {ExceptionType} mapped to {Status} for {Method} {Path} (traceId {TraceId}): {Message}",
            context.Exception.GetType().Name,
            statusCode,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            traceId,
            context.Exception.Message);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Detail = context.Exception.Message
        };
        problem.Extensions["traceId"] = traceId;

        context.Result = new ObjectResult(problem) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
