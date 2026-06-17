using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TelegramLike.Identity.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// HTTP responses, replacing the per-endpoint <c>SafeSend</c> try/catch helper.
/// Registered globally (see <c>Program.cs</c>) so every action is covered without
/// per-action wiring. Mapping intentionally matches the previous minimal-API behaviour:
/// <list type="bullet">
///   <item><see cref="ValidationException"/> (FluentValidation) → 400, joined error messages</item>
///   <item><see cref="InvalidOperationException"/> → 400, the exception message</item>
/// </list>
/// Anything else is left unhandled so it bubbles up as a 500.
/// <para>
/// Unlike the Chats <c>DomainExceptionFilter</c>, this returns the legacy
/// <c>{ "error": "..." }</c> body (not <see cref="ProblemDetails"/>) and also handles
/// FluentValidation's <see cref="ValidationException"/>. Both are required to keep the
/// Web BFF's Identity client working unchanged — it reads <c>error</c> off 400 responses
/// and surfaces the message on the register/login Razor pages.
/// </para>
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var error = context.Exception switch
        {
            ValidationException ex => string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
            InvalidOperationException ex => ex.Message,
            _ => null
        };

        if (error is null)
            return;

        context.Result = new ObjectResult(new { error })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };

        context.ExceptionHandled = true;
    }
}
