using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FluentValidation;

namespace TelegramLike.Messaging.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses, replacing the per-endpoint <c>SafeSend</c> /
/// <c>SafeSendVoid</c> try/catch helpers from the former minimal API. Registered globally
/// (see <c>Program.cs</c>) so every action is covered without per-action wiring.
/// <para>
/// Mapping intentionally matches the previous minimal-API behaviour exactly — both helpers
/// caught the same three exceptions and returned <c>Results.Problem(ex.Message, statusCode: …)</c>,
/// i.e. a <see cref="ProblemDetails"/> body whose <c>Detail</c> carries the message:
/// </para>
/// <list type="bullet">
///   <item><see cref="InvalidOperationException"/> → 400</item>
///   <item><see cref="ArgumentException"/> → 400</item>
///   <item><see cref="UnauthorizedAccessException"/> → 403</item>
/// </list>
/// Anything else is left unhandled so it bubbles up as a 500, exactly as before. The
/// fail-open membership path is untouched: Messaging never threw on missing membership, so
/// there is no mapping for it here either.
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => (int?)null
        };

        if (statusCode is null)
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Detail = context.Exception.Message
        })
        {
            StatusCode = statusCode
        };

        context.ExceptionHandled = true;
    }
}
