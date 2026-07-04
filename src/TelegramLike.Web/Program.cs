using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelegramLike.Web.Components;
using TelegramLike.Web.Services;
using TelegramLike.Web.Services.ChatChanged;
using TelegramLike.Web.Services.NewMessage;
using TelegramLike.Web.Services.ChatsApi;
using TelegramLike.Web.Services.IdentityApi;
using TelegramLike.Web.Services.MessagingApi;
using TelegramLike.Web.Services.NotificationsApi;
using TelegramLike.Web.Services.Presence;
using TelegramLike.Web.Services.PresenceApi;
using TelegramLike.Web.Services.Resilience;
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
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();

builder.Services.AddSingleton<ITypingPubSub, TypingPubSub>();
builder.Services.AddSingleton<INewMessagePubSub, NewMessagePubSub>();
builder.Services.AddSingleton<IUnreadCountPubSub, UnreadCountPubSub>();
builder.Services.AddSingleton<IChatChangedPubSub, ChatChangedPubSub>();
builder.Services.AddSingleton<IPresencePubSub, PresencePubSub>();

// The monolith is gone — the Web BFF now hosts its own MassTransit bus purely so the
// real-time pubsub consumers (typing, new-message, chat-changed, presence,
// unread-count) keep delivering integration events into the Blazor circuit.
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitVhost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<UserTypingConsumer>();
    bus.AddConsumer<NewMessageConsumer>();
    bus.AddConsumer<UnreadCountChangedConsumer>();
    bus.AddConsumer<MessageRetractedConsumer>();
    bus.AddConsumer<ReactionAddedConsumer>();
    bus.AddConsumer<ReactionRemovedConsumer>();
    bus.AddConsumer<UserCameOnlineConsumer>();
    bus.AddConsumer<UserWentOfflineConsumer>();

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

// Identity is the IdP. The Web no longer signs tokens; ServiceTokenProvider
// exchanges the current user's session token for a short-lived access JWT (cached)
// which each downstream client attaches itself. The exchange runs in the circuit
// scope (where the auth cookie is readable), so no DelegatingHandler is involved.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ServiceTokenProvider>();

var identityBaseUrl = builder.Configuration["IdentityApi:BaseUrl"]
                      ?? throw new InvalidOperationException("IdentityApi:BaseUrl is not configured.");

// Public auth client (no token) — also used by ServiceTokenProvider for the exchange.
builder.Services.AddHttpClient<IIdentityAuthApi, IdentityAuthApiClient>(client =>
    client.BaseAddress = new Uri(identityBaseUrl))
    .AddServiceResilience();
builder.Services.AddHttpClient<IIdentityUsersApi, IdentityUsersApiClient>(client =>
    client.BaseAddress = new Uri(identityBaseUrl))
    .AddServiceResilience();

builder.Services.AddHttpClient<INotificationsApi, NotificationsApiClient>(client =>
{
    var baseUrl = builder.Configuration["NotificationsApi:BaseUrl"]
                  ?? throw new InvalidOperationException("NotificationsApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddServiceResilience();

builder.Services.AddHttpClient<IPresenceApi, PresenceApiClient>(client =>
{
    var baseUrl = builder.Configuration["PresenceApi:BaseUrl"]
                  ?? throw new InvalidOperationException("PresenceApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddServiceResilience();

builder.Services.AddHttpClient<IChatsApi, ChatsApiClient>(client =>
{
    var baseUrl = builder.Configuration["ChatsApi:BaseUrl"]
                  ?? throw new InvalidOperationException("ChatsApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddServiceResilience();

builder.Services.AddHttpClient<IMessagingApi, MessagingApiClient>(client =>
{
    var baseUrl = builder.Configuration["MessagingApi:BaseUrl"]
                  ?? throw new InvalidOperationException("MessagingApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddServiceResilience();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Auth callback: Blazor Login page navigates here after obtaining a session token
app.MapGet("/auth/signin", async (
    string token,
    IIdentityAuthApi identity,
    HttpContext httpContext) =>
{
    // Exchange the session token at the IdP for the user's identity claims.
    var session = await identity.ExchangeAsync(token);
    if (session is null) return Results.Redirect("/login?error=invalid");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
        new(ClaimTypes.Name, session.Username),
        new(ClaimTypes.Email, session.Email),
        new("session_token", token)
    };
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    await httpContext.SignInAsync("Cookies", principal);

    return Results.Redirect("/");
});

app.MapGet("/auth/signout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync("Cookies");
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
