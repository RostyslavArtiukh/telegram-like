using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Gateway;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — routes/clusters generated from the backend list in GatewayRouting.
// The gateway itself does no auth: it forwards the Authorization header untouched and each
// service validates the Identity-issued JWT.
builder.Services.AddGatewayReverseProxy(builder.Configuration);

// Per-caller rate limiting ([TL-128]). The front door is the only place that sees every
// request, and until now nothing bounded how fast one client could call — one authenticated
// client in a loop could saturate Messaging and, through fan-out, the whole event chain.
builder.Services.AddGatewayRateLimiting(builder.Configuration);

// Trace the gateway hop too, so a request shows Web BFF -> gateway -> service in
// Jaeger. HttpClientInstrumentation captures the outbound proxied call.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.gateway",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Scrub the SignalR ?access_token= JWT from proxied hub-request spans.
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
            // YARP's own meter — request counts / latency per route + cluster.
            .AddMeter("Yarp.ReverseProxy")
            .AddPrometheusExporter();
    });

var app = builder.Build();

// Liveness only — the gateway has no backing store. Readiness for the services
// themselves stays each service's own /health/ready.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ok" }));

app.MapPrometheusScrapingEndpoint();

// Before the proxy, so a shed request never reaches a backend. Health and metrics are exempt
// inside the limiter itself rather than by ordering, so the exemption is stated where the
// policy is. Rejections surface as 429s in the existing RED dashboard; they are deliberately
// not 5xx, so HighHttp5xxRate does not fire on a client being told to slow down.
app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
