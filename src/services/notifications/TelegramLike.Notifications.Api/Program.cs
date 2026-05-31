using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.notifications",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("MassTransit");

        var otlpEndpoint = builder.Configuration["Tracing:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

// MassTransit auto-registers a "masstransit-bus" health check with the "ready"
// tag (it goes Healthy once the RabbitMQ bus is connected) — we only need to
// add the database probe ourselves. Avoiding AspNetCore.HealthChecks.Rabbitmq
// here also dodges a RabbitMQ.Client major-version clash with MassTransit 8.3.
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoClient>(),
        name: "mongo",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Liveness: the process is up and the pipeline responds. No external probes.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: all downstream dependencies (Mongo, RabbitMQ) are reachable.
// docker-compose `depends_on: condition: service_healthy` waits on this.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready")
});

// Legacy alias kept so callers that already use /health keep working.
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
