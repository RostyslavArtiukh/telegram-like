using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Identity.Api.Filters;
using TelegramLike.Identity.Application.Commands.RegisterUser;
using TelegramLike.Identity.Application.Common.Behaviors;
using TelegramLike.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddControllers(options => options.Filters.Add<DomainExceptionFilter>());

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
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
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

app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();
