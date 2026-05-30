using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TelegramLike.Presence.Application.Commands.GoOffline;
using TelegramLike.Presence.Application.Commands.Heartbeat;
using TelegramLike.Presence.Application.Commands.StartTyping;
using TelegramLike.Presence.Application.Commands.StopTyping;
using TelegramLike.Presence.Application.Queries.GetTypingUsers;
using TelegramLike.Presence.Application.Queries.GetUserPresence;
using TelegramLike.Presence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(HeartbeatCommand).Assembly));

builder.Services.AddPresenceInfrastructure(builder.Configuration);

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

var presence = app.MapGroup("/presence").RequireAuthorization();

presence.MapPost("/heartbeat", async (
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    await mediator.Send(new HeartbeatCommand(userId), ct);
    return Results.NoContent();
});

presence.MapPost("/offline", async (
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    await mediator.Send(new GoOfflineCommand(userId), ct);
    return Results.NoContent();
});

presence.MapGet("/{userId:guid}", async (
    Guid userId,
    IMediator mediator,
    CancellationToken ct) =>
{
    var dto = await mediator.Send(new GetUserPresenceQuery(userId), ct);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

presence.MapPost("/typing/{chatId:guid}/start", async (
    Guid chatId,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    await mediator.Send(new StartTypingCommand(chatId, userId), ct);
    return Results.NoContent();
});

presence.MapPost("/typing/{chatId:guid}/stop", async (
    Guid chatId,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    await mediator.Send(new StopTypingCommand(chatId, userId), ct);
    return Results.NoContent();
});

presence.MapGet("/typing/{chatId:guid}", async (
    Guid chatId,
    IMediator mediator,
    CancellationToken ct) =>
{
    var dto = await mediator.Send(new GetTypingUsersQuery(chatId), ct);
    return Results.Ok(dto);
});

presence.MapPost("/batch", async (
    Guid[] userIds,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(
        new TelegramLike.Presence.Application.Queries.GetBatchPresence.GetBatchPresenceQuery(userIds), ct);
    return Results.Ok(result);
});

app.Run();

static bool TryGetUserId(HttpContext httpContext, out Guid userId)
{
    userId = Guid.Empty;
    var sub = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
              ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId);
}
