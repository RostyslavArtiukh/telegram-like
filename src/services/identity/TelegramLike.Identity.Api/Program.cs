using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Identity.Application.Auth.ExchangeSession;
using TelegramLike.Identity.Application.Commands.LoginUser;
using TelegramLike.Identity.Application.Commands.RegisterUser;
using TelegramLike.Identity.Application.Common.Behaviors;
using TelegramLike.Identity.Application.Queries.GetUserById;
using TelegramLike.Identity.Application.Queries.GetUserIdByUsername;
using TelegramLike.Identity.Application.Queries.GetUsernamesByIds;
using TelegramLike.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

builder.Services.AddIdentityInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["ServiceAuth:JwtSecret"]
                ?? throw new InvalidOperationException("ServiceAuth:JwtSecret is not configured.");
var jwtIssuer = builder.Configuration["ServiceAuth:Issuer"]
                ?? throw new InvalidOperationException("ServiceAuth:Issuer is not configured.");
var jwtAudience = builder.Configuration["ServiceAuth:Audience"]
                  ?? throw new InvalidOperationException("ServiceAuth:Audience is not configured.");

// Identity is the IdP, so it validates the very tokens it issues (issuer = telegramlike-identity).
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

var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
                            ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.identity",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        var otlpEndpoint = builder.Configuration["Tracing:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

// Identity has no message bus, so only Mongo + Redis are probed.
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoClient>(),
        name: "mongo",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" })
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ---- Public auth endpoints (no bearer — the caller isn't authenticated yet) ----
var auth = app.MapGroup("/auth");

auth.MapPost("/register", (RegisterRequest body, IMediator mediator, CancellationToken ct) =>
    SafeSend(mediator,
        new RegisterUserCommand(body.Email, body.Username, body.DisplayName, body.Password),
        id => Results.Ok(new { userId = id }), ct));

auth.MapPost("/login", (LoginRequest body, IMediator mediator, CancellationToken ct) =>
    SafeSend(mediator,
        new LoginUserCommand(body.Email, body.Password),
        token => Results.Ok(new { sessionToken = token }), ct));

// Exchange an opaque session token for a short-lived access JWT + identity claims.
// Possession of a valid session token is the credential, so this stays public.
auth.MapPost("/token", async (TokenRequest body, IMediator mediator, CancellationToken ct) =>
{
    var dto = await mediator.Send(new ExchangeSessionQuery(body.SessionToken), ct);
    return dto is null ? Results.Unauthorized() : Results.Ok(dto);
});

// ---- Authenticated user queries (downstream callers present an Identity-issued JWT) ----
var users = app.MapGroup("/users").RequireAuthorization();

users.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var dto = await mediator.Send(new GetUserByIdQuery(id), ct);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

users.MapPost("/by-ids", async (Guid[] ids, IMediator mediator, CancellationToken ct) =>
{
    var map = await mediator.Send(new GetUsernamesByIdsQuery(ids), ct);
    return Results.Ok(map);
});

users.MapGet("/by-username", async (string u, IMediator mediator, CancellationToken ct) =>
{
    var userId = await mediator.Send(new GetUserIdByUsernameQuery(u), ct);
    return userId is null ? Results.NotFound() : Results.Ok(new { userId });
});

app.Run();

// Maps handler exceptions to 400 so the Web BFF can surface validation / business errors.
static async Task<IResult> SafeSend<TResult>(
    IMediator mediator, IRequest<TResult> request, Func<TResult, IResult> onSuccess, CancellationToken ct)
{
    try
    {
        return onSuccess(await mediator.Send(request, ct));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)) });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

internal sealed record RegisterRequest(string Email, string Username, string DisplayName, string Password);
internal sealed record LoginRequest(string Email, string Password);
internal sealed record TokenRequest(string SessionToken);
