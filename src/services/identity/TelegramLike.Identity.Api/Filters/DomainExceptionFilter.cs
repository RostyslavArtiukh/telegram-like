using TelegramLike.Domain.ServiceDefaults;
using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TelegramLike.Identity.Domain;

namespace TelegramLike.Identity.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// HTTP responses, replacing the per-endpoint <c>SafeSend</c> try/catch helper.
/// Registered globally (see <c>Program.cs</c>) so every action is covered without
/// per-action wiring. Mapping:
/// <list type="bullet">
///   <item><see cref="ValidationException"/> (FluentValidation) → 400, joined error messages</item>
///   <item><see cref="DomainException"/> → 400, the exception message</item>
/// </list>
/// Anything else is left unhandled so it bubbles up as a 500. Since [TL-98] the value-object
/// guards (Username/Email/DisplayName/HashedPassword) throw <see cref="DomainException"/> and
/// therefore surface as a 400 with the guard message; framework-thrown exceptions (LINQ, the
/// Mongo driver) keep bubbling up as a 500 and are never mislabelled as a client 400.
/// <para>
/// Unlike the Chats <c>DomainExceptionFilter</c>, this returns the legacy
/// <c>{ "error": "..." }</c> body (not <see cref="ProblemDetails"/>) and also handles
/// FluentValidation's <see cref="ValidationException"/>. Both are required to keep the
/// Web BFF's Identity client working unchanged — it reads <c>error</c> off 400 responses
/// and surfaces the message on the register/login Razor pages.
/// </para>
/// <para>
/// Every mapped exception is logged (previously these were swallowed silently, invisible in
/// logs and traces) with the current trace id so it can be correlated in Jaeger. The response
/// body is left as the legacy <c>{ error }</c> shape — the trace id lives in logs only.
/// </para>
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var error = context.Exception switch
        {
            ValidationException ex => string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
            DomainException ex => ex.Message,
            _ => null
        };

        if (error is null)
            return;

        var traceId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        logger.LogWarning(
            "Domain exception {ExceptionType} mapped to 400 for {Method} {Path} (traceId {TraceId}): {Message}",
            context.Exception.GetType().Name,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            traceId,
            error);

        context.Result = new ObjectResult(new { error })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };

        context.ExceptionHandled = true;
    }
}
