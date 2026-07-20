using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Chats.Api.Filters;
using TelegramLike.Chats.Application.Observability;
using TelegramLike.Chats.Application.Queries.GetMyChats;
using TelegramLike.Chats.Infrastructure;
using TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetMyChatsQuery).Assembly));

builder.Services.AddChatsInfrastructure(builder.Configuration);

// Product counters the handlers write to. Singleton because a Meter is meant to be
// created once per process, not per request.
builder.Services.AddSingleton<ChatsMetrics>();

builder.Services
    .AddControllers(options => options.Filters.Add<DomainExceptionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddServiceJwtAuth(builder.Configuration);

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
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            // Custom meters must be named explicitly — anything not listed is dropped.
            .AddMeter(ChatsMetrics.MeterName)
            .AddMeter(OutboxMetrics.MeterName)
            .AddView(
                "telegramlike.outbox.publish_delay",
                new ExplicitBucketHistogramConfiguration { Boundaries = OutboxMetrics.PublishDelayBucketsSeconds })
            .AddPrometheusExporter();
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

app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();

public partial class Program; // hook for WebApplicationFactory<Program> in Api tests
