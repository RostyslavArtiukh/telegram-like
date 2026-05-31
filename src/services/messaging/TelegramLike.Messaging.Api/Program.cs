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
using TelegramLike.Messaging.Application.Commands.AddReaction;
using TelegramLike.Messaging.Application.Commands.HideMessage;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;
using TelegramLike.Messaging.Application.Commands.RemoveReaction;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Application.Commands.SendMessage;
using TelegramLike.Messaging.Application.Queries.GetChatMessages;
using TelegramLike.Messaging.Application.Queries.GetMessageById;
using TelegramLike.Messaging.Domain.ValueObjects;
using TelegramLike.Messaging.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SendMessageCommand).Assembly));

builder.Services.AddMessagingInfrastructure(builder.Configuration);

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
        serviceName: "telegramlike.messaging",
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

var messages = app.MapGroup("/messages").RequireAuthorization();

messages.MapPost("/", async (
    SendMessageRequest body, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();

    var attachments = body.Attachments?
        .Select(a => new SendMessageAttachment(a.Type, a.Url, a.SizeBytes, a.FileName))
        .ToList();

    return await SafeSend(() => mediator.Send(new SendMessageCommand(
            body.ChatId,
            userId,
            body.Text,
            body.Recipients,
            body.IsBroadcast,
            attachments,
            body.ReplyToMessageId,
            body.ForwardOriginalMessageId,
            body.ForwardOriginalChatId), ct),
        id => Results.Created($"/messages/{id}", new MessageCreatedResponse(id)));
});

messages.MapGet("/{messageId:guid}", async (
    Guid messageId, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    var result = await mediator.Send(new GetMessageByIdQuery(messageId, userId), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

messages.MapPost("/{messageId:guid}/reactions", async (
    Guid messageId,
    AddReactionRequest body,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() =>
        mediator.Send(new AddReactionCommand(messageId, userId, body.Emoji, body.ActorIsPremium), ct));
});

messages.MapDelete("/{messageId:guid}/reactions/{emoji}", async (
    Guid messageId,
    string emoji,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    if (!Enum.TryParse<Emoji>(emoji, ignoreCase: true, out var parsed))
        return Results.Problem("Unknown emoji.", statusCode: StatusCodes.Status400BadRequest);

    return await SafeSendVoid(() =>
        mediator.Send(new RemoveReactionCommand(messageId, userId, parsed), ct));
});

messages.MapPost("/{messageId:guid}/retract", async (
    Guid messageId,
    RetractMessageRequest body,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() =>
        mediator.Send(new RetractMessageCommand(messageId, userId, body.ActorIsModerator), ct));
});

messages.MapPost("/{messageId:guid}/read", async (
    Guid messageId,
    MarkAsReadRequest body,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() =>
        mediator.Send(new MarkMessageAsReadCommand(messageId, userId, body.IsBroadcast), ct));
});

messages.MapPost("/{messageId:guid}/hide", async (
    Guid messageId, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    return await SafeSendVoid(() => mediator.Send(new HideMessageCommand(messageId, userId), ct));
});

var chats = app.MapGroup("/chats").RequireAuthorization();

chats.MapGet("/{chatId:guid}/messages", async (
    Guid chatId,
    DateTime? before,
    int? pageSize,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
    var result = await mediator.Send(
        new GetChatMessagesQuery(chatId, userId, before, pageSize ?? 50), ct);
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

public sealed record SendMessageAttachmentDto(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record SendMessageRequest(
    Guid ChatId,
    string? Text,
    IReadOnlyList<Guid> Recipients,
    bool IsBroadcast,
    IReadOnlyList<SendMessageAttachmentDto>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null);

public sealed record AddReactionRequest(Emoji Emoji, bool ActorIsPremium);
public sealed record RetractMessageRequest(bool ActorIsModerator);
public sealed record MarkAsReadRequest(bool IsBroadcast);
public sealed record MessageCreatedResponse(Guid MessageId);
