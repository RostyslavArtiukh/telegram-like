using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — routes and clusters come entirely from the "ReverseProxy"
// config section (appsettings + env overrides), so adding/retargeting a service
// is a config change, not code. The gateway itself does no auth: it forwards the
// Authorization header untouched and each service validates the Identity-issued JWT.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Trace the gateway hop too, so a request shows Web BFF -> gateway -> service in
// Jaeger. HttpClientInstrumentation captures the outbound proxied call.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.gateway",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        var otlpEndpoint = builder.Configuration["Tracing:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

var app = builder.Build();

// Liveness only — the gateway has no backing store. Readiness for the services
// themselves stays each service's own /health/ready.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ok" }));

app.MapReverseProxy();

app.Run();
