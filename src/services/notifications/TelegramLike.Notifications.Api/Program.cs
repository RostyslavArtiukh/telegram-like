using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TelegramLike.Notifications.Api.Mapping;
using TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;
using TelegramLike.Notifications.Application.Commands.MarkChatNotificationsAsRead;
using TelegramLike.Notifications.Application.Commands.MarkNotificationAsRead;
using TelegramLike.Notifications.Application.Queries.GetNotificationFeed;
using TelegramLike.Notifications.Application.Queries.GetUnreadCount;
using TelegramLike.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetNotificationFeedQuery).Assembly));

builder.Services.AddNotificationsInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["ServiceAuth:JwtSecret"]
                ?? throw new InvalidOperationException("ServiceAuth:JwtSecret is not configured.");
var jwtIssuer = builder.Configuration["ServiceAuth:Issuer"]
                ?? throw new InvalidOperationException("ServiceAuth:Issuer is not configured.");
var jwtAudience = builder.Configuration["ServiceAuth:Audience"]
                  ?? throw new InvalidOperationException("ServiceAuth:Audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var notifications = app.MapGroup("/notifications").RequireAuthorization();

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

notifications.MapPost("/chats/{chatId:guid}/read", async (
    Guid chatId,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId))
        return Results.Unauthorized();

    await mediator.Send(new MarkChatNotificationsAsReadCommand(userId, chatId), ct);
    return Results.NoContent();
});

app.Run();

static bool TryGetUserId(HttpContext httpContext, out Guid userId)
{
    userId = Guid.Empty;
    var sub = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
              ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId);
}

public sealed record UnreadCountResponse(long Count);
