using TelegramLike.Domain.ServiceDefaults;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using FluentValidation;
using TelegramLike.Messaging.Domain;

namespace TelegramLike.Messaging.Api.Filters;

/// <summary>
/// Translates deliberate domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses, replacing the per-endpoint <c>SafeSend</c> /
/// <c>SafeSendVoid</c> try/catch helpers from the former minimal API. Registered globally
/// (see <c>Program.cs</c>) so every action is covered without per-action wiring:
/// <list type="bullet">
///   <item><see cref="FluentValidation.ValidationException"/> → 400 (request validation)</item>
///   <item><see cref="ForbiddenException"/> → 403</item>
///   <item><see cref="DomainException"/> (its base) → 400</item>
/// </list>
/// Anything else — including framework-thrown <see cref="InvalidOperationException"/> /
/// <see cref="ArgumentException"/> (LINQ, the Mongo driver) — is left unhandled so it bubbles up
/// as a <c>500</c>. This is the deliberate change from the previous filter, which caught those
/// raw BCL base types and mislabelled such server bugs as a client <c>400</c> with an internal
/// message in the body. The fail-open membership path is untouched: Messaging never threw on
/// missing membership, so there is no mapping for it here either. Mapped exceptions are logged
/// and the response carries the current trace id (<c>traceId</c> extension) so a client-reported
/// error correlates with its Jaeger trace.
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
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
