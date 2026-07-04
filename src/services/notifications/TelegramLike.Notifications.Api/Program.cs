using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Notifications.Api.Filters;
using TelegramLike.Notifications.Application.Queries.GetNotificationFeed;
using TelegramLike.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetNotificationFeedQuery).Assembly));

builder.Services.AddNotificationsInfrastructure(builder.Configuration);

// Classic MVC controllers with a global filter that maps domain exceptions to the same
// status/body the former minimal API emitted. No JSON enum converter is registered here:
// notifications serializes its contract enums (NotificationType/Status) as numbers, and
// adding a string converter would change the wire contract the Web BFF client reads.
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
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
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

app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();
