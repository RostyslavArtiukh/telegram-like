using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Realtime.Api.Consumers;
using TelegramLike.Realtime.Api.Hubs;
using TelegramLike.Realtime.Api.Membership;
using TelegramLike.Realtime.Api.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatMembershipTracker>();

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

        // Browsers can't set headers on WebSocket upgrades, so SignalR clients send
        // the JWT as ?access_token=. Only honored for the hub path.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitVhost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

// Same fan-out model as the Web BFF ([TL-63]): these consumers only push into
// hub connections held by THIS instance, so every replica needs its own copy of
// every event — per-instance temporary queues, not a shared durable queue that
// RabbitMQ would round-robin.
var busInstanceId = Guid.NewGuid().ToString("N");
void PerInstanceQueue(IEndpointRegistrationConfigurator e)
{
    e.Temporary = true;
    e.InstanceId = busInstanceId;
}

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<MessageSentConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<MessageRetractedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<ReactionAddedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<ReactionRemovedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UserTypingConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UserCameOnlineConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UserWentOfflineConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UnreadCountChangedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<MemberJoinedMembershipConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<MemberLeftMembershipConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<MemberKickedMembershipConsumer>().Endpoint(PerInstanceQueue);
    // Backfill snapshots make JoinChat fail-closed for pre-existing chats ([TL-103]).
    bus.AddConsumer<ChatMembershipsSnapshotMembershipConsumer>().Endpoint(PerInstanceQueue);

    bus.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVhost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.realtime",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("MassTransit")
            // Scrub the SignalR ?access_token= JWT from hub-request spans.
            .AddProcessor(new RedactAccessTokenProcessor());

        var otlpEndpoint = builder.Configuration["Tracing:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

// No database here — readiness is just the RabbitMQ bus, and MassTransit
// auto-registers its "masstransit-bus" check with the "ready" tag.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready")
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPrometheusScrapingEndpoint();

app.MapHub<RealtimeHub>("/hub");

app.Run();
