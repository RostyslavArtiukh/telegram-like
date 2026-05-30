using MediatR;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Api.Mapping;
using TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;
using TelegramLike.Notifications.Application.Commands.MarkNotificationAsRead;
using TelegramLike.Notifications.Application.Queries.GetNotificationFeed;
using TelegramLike.Notifications.Application.Queries.GetUnreadCount;
using TelegramLike.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetNotificationFeedQuery).Assembly));

builder.Services.AddNotificationsInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var notifications = app.MapGroup("/notifications");

notifications.MapGet("/", async (
    HttpContext httpContext,
    IMediator mediator,
    DateTime? before,
    int? pageSize,
    bool? unreadOnly,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId))
        return Results.Unauthorized();

    var result = await mediator.Send(
        new GetNotificationFeedQuery(userId, before, pageSize ?? 20, unreadOnly ?? false), ct);

    return Results.Ok(result.ToContract());
});

notifications.MapGet("/unread-count", async (
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId))
        return Results.Unauthorized();

    var count = await mediator.Send(new GetUnreadCountQuery(userId), ct);
    return Results.Ok(new UnreadCountResponse(count));
});

notifications.MapPost("/{id:guid}/read", async (
    Guid id,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId))
        return Results.Unauthorized();

    try
    {
        await mediator.Send(new MarkNotificationAsReadCommand(id, userId), ct);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

notifications.MapPost("/read-all", async (
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId))
        return Results.Unauthorized();

    await mediator.Send(new MarkAllNotificationsAsReadCommand(userId), ct);
    return Results.NoContent();
});

app.Run();

static bool TryGetUserId(HttpContext httpContext, out Guid userId)
{
    userId = Guid.Empty;
    var header = httpContext.Request.Headers["X-User-Id"].ToString();
    return !string.IsNullOrWhiteSpace(header) && Guid.TryParse(header, out userId);
}

public sealed record UnreadCountResponse(long Count);
