using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TelegramLike.Notifications.Api.Filters;

/// <summary>
/// Translates domain/application exceptions thrown by command + query handlers into
/// <see cref="ProblemDetails"/> responses, replacing the per-endpoint try/catch from the
/// former minimal API. Registered globally (see <c>Program.cs</c>) so every action is
/// covered without per-action wiring.
/// <para>
/// Mapping intentionally matches the previous minimal-API behaviour exactly: only the
/// <c>POST /notifications/{id}/read</c> endpoint caught <see cref="InvalidOperationException"/>
/// and returned <c>Results.Problem(ex.Message, statusCode: 400)</c> — a 400 with a
/// <see cref="ProblemDetails"/> body. No other endpoint caught anything, so every other
/// exception (including <see cref="ArgumentException"/> from the mark-read commands) was
/// left to bubble up as a 500. We preserve that here: <see cref="InvalidOperationException"/>
/// → 400 <see cref="ProblemDetails"/>, everything else unhandled → 500.
/// </para>
/// <remarks>
/// Unlike the Chats <c>DomainExceptionFilter</c> this does NOT map <see cref="ArgumentException"/>
/// (→400) or <see cref="UnauthorizedAccessException"/> (→403): notifications never emitted those
/// status codes from its API, and mapping them would silently change the wire contract.
/// </remarks>
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            InvalidOperationException => StatusCodes.Status400BadRequest,
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
