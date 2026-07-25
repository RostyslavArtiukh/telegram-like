using TelegramLike.Shared.Domain;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TelegramLike.Notifications.Domain;

namespace TelegramLike.Notifications.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses, replacing the per-endpoint try/catch from the
/// former minimal API. Registered globally (see <c>Program.cs</c>) so every action is
/// covered without per-action wiring.
/// <para>
/// Only deliberate <see cref="DomainException"/>s (business-rule violations from the mark-read
/// command, e.g. "notification not found", "cannot mark another user's notification as read")
/// map to <c>400</c>. This preserves the previous behaviour — the only endpoint that emitted a
/// 400 was <c>POST /notifications/{id}/read</c> — while fixing its flaw: it used to catch the
/// raw <see cref="InvalidOperationException"/> base type, so a framework-thrown one (LINQ, the
/// Mongo driver, an enum-mapping default case) would have been mislabelled as a client 400 with
/// an internal message. Those now bubble up as a <c>500</c>.
/// </para>
/// <remarks>
/// Unlike Chats this maps no <c>403</c> — notifications never emitted it. Since [TL-98] the
/// Domain/Application guards (empty recipient/chat ids) throw <see cref="DomainException"/> and
/// surface as a 400; only framework-thrown exceptions stay a 500 (e.g. the enum-mapping default
/// cases, which guard data integrity, not client input). Mapped exceptions are logged and the
/// response carries the current trace id
/// (<c>traceId</c> extension) so a client-reported error correlates with its Jaeger trace.
/// </remarks>
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
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
