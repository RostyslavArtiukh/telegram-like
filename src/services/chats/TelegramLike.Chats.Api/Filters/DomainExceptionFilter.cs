using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TelegramLike.Chats.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses, replacing the per-endpoint try/catch helpers.
/// Registered globally (see <c>Program.cs</c>) so every action is covered without
/// per-action wiring. Mapping intentionally matches the previous minimal-API behaviour:
/// <list type="bullet">
///   <item><see cref="InvalidOperationException"/> → 400</item>
///   <item><see cref="ArgumentException"/> → 400</item>
///   <item><see cref="UnauthorizedAccessException"/> → 403</item>
/// </list>
/// Anything else is left unhandled so it bubbles up as a 500.
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
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
