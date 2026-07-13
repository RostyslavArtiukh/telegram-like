using MassTransit;
using MudBlazor.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Client;
using TelegramLike.Client.Auth;
using TelegramLike.Web.Components;
using TelegramLike.Web.Services;
using TelegramLike.Web.Services.ChatChanged;
using TelegramLike.Web.Services.NewMessage;
using TelegramLike.Web.Services.Presence;
using TelegramLike.Web.Services.ServiceAuth;
using TelegramLike.Web.Services.Typing;
using TelegramLike.Web.Services.UnreadCount;

var builder = WebApplication.CreateBuilder(args);

// Persist DataProtection keys across container restarts. Without this, every
// rebuild generates ephemeral keys → existing auth cookies and antiforgery
// tokens become undecryptable and every user has to re-login.
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
                         ?? Path.Combine(AppContext.BaseDirectory, "dp-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("TelegramLike.Web");

// End-to-end traces from this Web BFF down to Notifications/Presence and through
// RabbitMQ. MassTransit publishes its own ActivitySource named "MassTransit", so
// adding it as a source captures outbox-publish + consumer spans automatically.
// OTLP endpoint defaults to the Jaeger sidecar in docker-compose; for local
// `dotnet run` without compose, leave Tracing:OtlpEndpoint unset and tracing
// becomes a no-op exporter (won't fail).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "telegramlike.web",
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
            // Polly's meter — the resilience pipeline emits retry attempts and
            // circuit-breaker state transitions here, so a tripped breaker is visible.
            .AddMeter("Polly")
            .AddPrometheusExporter();
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

// Controllers back the /auth callbacks (Controllers/AuthController.cs) — the only
// classic HTTP endpoints in this Blazor host; everything else is Razor components.
builder.Services.AddControllers();

// MudBlazor: dialogs, snackbars, popovers, theming. The whole UI runs
// InteractiveServer (set on <Routes> in App.razor) so these services have a live
// circuit to talk to.
builder.Services.AddMudServices();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.HttpOnly = true;
        // There's TLS in front (gateway/ingress), so the auth cookie should never be sent
        // in the clear; explicit SameSite=Lax keeps normal top-level navigation (e.g. the
        // /auth/signin redirect) working while still blocking cross-site form submission.
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();

builder.Services.AddSingleton<TypingPubSub>();
builder.Services.AddSingleton<NewMessagePubSub>();
builder.Services.AddSingleton<UnreadCountPubSub>();
builder.Services.AddSingleton<ChatChangedPubSub>();
builder.Services.AddSingleton<PresencePubSub>();

// The monolith is gone — the Web BFF now hosts its own MassTransit bus purely so the
// real-time pubsub consumers (typing, new-message, chat-changed, presence,
// unread-count) keep delivering integration events into the Blazor circuit.
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitVhost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

// Real-time fan-out to EVERY Web instance. These consumers only push into the
// local Blazor circuits, so with >1 Web replica each instance must receive its
// own copy of every event. A shared (durable) queue per consumer would make
// RabbitMQ round-robin the events — only one replica would get each one, and
// users whose circuit lives on another replica would miss the update.
//
// Fix: give every replica its own queue. `InstanceId` (unique per process)
// makes the queue name unique; `Temporary = true` marks it non-durable +
// auto-delete so it disappears when the pod stops. Both queues bind to the same
// message-type exchange, so RabbitMQ fans each event out to all replicas.
// (The 5 backend services keep shared durable queues — a read-model must
// process each event once, not once-per-replica.)
var busInstanceId = Guid.NewGuid().ToString("N");
void PerInstanceQueue(IEndpointRegistrationConfigurator e)
{
    e.Temporary = true;
    e.InstanceId = busInstanceId;
}

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<UserTypingConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<NewMessageConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UnreadCountChangedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<MessageRetractedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<ReactionAddedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<ReactionRemovedConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UserCameOnlineConsumer>().Endpoint(PerInstanceQueue);
    bus.AddConsumer<UserWentOfflineConsumer>().Endpoint(PerInstanceQueue);

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

// Identity is the IdP. The Web signs nothing; ServiceTokenProvider (the BFF's
// IAccessTokenProvider) exchanges the current user's session token for a short-lived
// access JWT (cached) which each SDK client attaches itself. The exchange runs in the
// circuit scope (where the auth cookie is readable), so no DelegatingHandler is involved.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAccessTokenProvider, ServiceTokenProvider>();

// All downstream calls go through the single YARP gateway address via the
// TelegramLike.Client SDK: one base URL, per-service prefixes (which the gateway
// strips and routes on) and the shared resilience pipeline live in the SDK.
var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"]
                     ?? throw new InvalidOperationException("Gateway:BaseUrl is not configured.");
builder.Services.AddTelegramLikeApiClients(new Uri(gatewayBaseUrl));

// MassTransit auto-registers its "masstransit-bus" health check with the "ready" tag —
// the BFF's only stateful dependency. The gateway is deliberately NOT probed: a downstream
// outage is absorbed by the SDK resilience pipeline + graceful degradation, and gating
// readiness on it would pull web out of the load balancer exactly when it can still serve.
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Liveness: the process is up. Readiness: the RabbitMQ bus is connected (tagged "ready"
// by MassTransit). Same endpoint contract as the 5 services + realtime — used by the
// compose healthcheck and the k8s probes.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// Auth callbacks (/auth/signin, /auth/signout) live in Controllers/AuthController.cs,
// matching the 5 services' convention that HTTP endpoints don't sit inline in Program.cs.
app.MapControllers();

app.MapPrometheusScrapingEndpoint();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
