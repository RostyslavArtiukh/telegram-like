using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;
using TelegramLike.Chats.Application.Commands.CreateDirectChat;
using TelegramLike.Chats.Application.Commands.CreateGroupChat;
using TelegramLike.Chats.Application.Commands.JoinChat;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Application.Commands.LeaveChat;
using TelegramLike.Chats.Application.Commands.RenameChat;
using TelegramLike.Chats.Application.Commands.TransferOwnership;
using TelegramLike.Chats.Application.Queries.GetChatById;
using TelegramLike.Chats.Application.Queries.GetChatMembers;
using TelegramLike.Chats.Application.Queries.GetMyChats;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetMyChatsQuery).Assembly));

builder.Services.AddChatsInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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
        serviceName: "telegramlike.chats",
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

builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoClient>(),
        name: "mongo",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var chats = app.MapGroup("/chats").RequireAuthorization();

chats.MapGet("/my", async (HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    var result = await mediator.Send(new GetMyChatsQuery(userId), ct);
    return Results.Ok(result);
});

chats.MapGet("/{chatId:guid}", async (Guid chatId, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetChatByIdQuery(chatId), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

chats.MapGet("/{chatId:guid}/members", async (Guid chatId, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetChatMembersQuery(chatId), ct);
    return Results.Ok(result);
});

chats.MapPost("/direct", async (
    CreateDirectChatRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSend(() => mediator.Send(new CreateDirectChatCommand(userId, body.PeerUserId), ct),
        id => Results.Created($"/chats/{id}", new ChatCreatedResponse(id)));
});

chats.MapPost("/group", async (
    CreateGroupChatRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSend(() => mediator.Send(new CreateGroupChatCommand(userId, body.Name), ct),
        id => Results.Created($"/chats/{id}", new ChatCreatedResponse(id)));
});

chats.MapPost("/broadcast", async (
    CreateBroadcastChannelRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSend(() => mediator.Send(new CreateBroadcastChannelCommand(userId, body.Name), ct),
        id => Results.Created($"/chats/{id}", new ChatCreatedResponse(id)));
});

chats.MapPost("/{chatId:guid}/join", async (
    Guid chatId, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() => mediator.Send(new JoinChatCommand(chatId, userId), ct));
});

chats.MapPost("/{chatId:guid}/leave", async (
    Guid chatId, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() => mediator.Send(new LeaveChatCommand(chatId, userId), ct));
});

chats.MapPost("/{chatId:guid}/members/{targetUserId:guid}/kick", async (
    Guid chatId, Guid targetUserId, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var actorId)) return Results.Unauthorized();
    return await SafeSendVoid(() => mediator.Send(new KickMemberCommand(chatId, targetUserId, actorId), ct));
});

chats.MapPost("/{chatId:guid}/members/{targetUserId:guid}/role", async (
    Guid chatId,
    Guid targetUserId,
    ChangeMemberRoleRequest body,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var actorId)) return Results.Unauthorized();
    return await SafeSendVoid(() =>
        mediator.Send(new ChangeMemberRoleCommand(chatId, targetUserId, body.NewRole, actorId), ct));
});

chats.MapPost("/{chatId:guid}/transfer-ownership", async (
    Guid chatId, TransferOwnershipRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var actorId)) return Results.Unauthorized();
    return await SafeSendVoid(() =>
        mediator.Send(new TransferOwnershipCommand(chatId, body.NewOwnerUserId, actorId), ct));
});

chats.MapPatch("/{chatId:guid}", async (
    Guid chatId, RenameChatRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var actorId)) return Results.Unauthorized();
    return await SafeSendVoid(() => mediator.Send(new RenameChatCommand(chatId, body.NewName, actorId), ct));
});

app.Run();

static bool TryGetUserId(HttpContext httpContext, out Guid userId)
{
    userId = Guid.Empty;
    var sub = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
              ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId);
}

static async Task<IResult> SafeSend<T>(Func<Task<T>> action, Func<T, IResult> onSuccess)
{
    try { return onSuccess(await action()); }
    catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest); }
    catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}

static async Task<IResult> SafeSendVoid(Func<Task> action)
{
    try { await action(); return Results.NoContent(); }
    catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest); }
    catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest); }
    catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
}

public sealed record CreateDirectChatRequest(Guid PeerUserId);
public sealed record CreateGroupChatRequest(string Name);
public sealed record CreateBroadcastChannelRequest(string Name);
public sealed record ChangeMemberRoleRequest(MemberRole NewRole);
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);
public sealed record RenameChatRequest(string NewName);
public sealed record ChatCreatedResponse(Guid ChatId);
