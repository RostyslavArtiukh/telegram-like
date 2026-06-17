using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Presence.Api.Filters;
using TelegramLike.Presence.Application.Commands.Heartbeat;
using TelegramLike.Presence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(HeartbeatCommand).Assembly));

builder.Services.AddPresenceInfrastructure(builder.Configuration);

// Classic MVC controllers with a global exception filter that mirrors the Chats/Identity/
// Notifications structure. No JSON enum converter is registered here: presence serializes its
// OnlineStatus enum as a number (0=Offline, 1=Online) and the Web BFF Presence client reads
// `status` as an int — adding a string converter would change that wire contract.
builder.Services
    .AddControllers(options => options.Filters.Add<DomainExceptionFilter>());

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

var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
                            ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.presence",
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
// tag, so we only add Mongo and Redis probes here. Avoiding the AspNetCore
// RabbitMQ health-check package dodges a version clash with MassTransit 8.3.
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

// Liveness: the process is up. No external probes.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: all downstream dependencies (Mongo, RabbitMQ, Redis) are reachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready")
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapControllers();

app.Run();
