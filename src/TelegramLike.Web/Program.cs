using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Application.Identity.Commands.RegisterUser;
using TelegramLike.Application.Identity.Queries.GetUserById;
using TelegramLike.Infrastructure;
using TelegramLike.Web.Components;
using TelegramLike.Web.Services;
using TelegramLike.Web.Services.NewMessage;
using TelegramLike.Web.Services.NotificationsApi;
using TelegramLike.Web.Services.PresenceApi;
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

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

builder.Services.AddSingleton<ITypingPubSub, TypingPubSub>();
builder.Services.AddSingleton<INewMessagePubSub, NewMessagePubSub>();
builder.Services.AddSingleton<IUnreadCountPubSub, UnreadCountPubSub>();

builder.Services.AddInfrastructure(builder.Configuration, bus =>
{
    bus.AddConsumer<UserTypingConsumer>();
    bus.AddConsumer<NewMessageConsumer>();
    bus.AddConsumer<UnreadCountChangedConsumer>();
});

builder.Services.Configure<ServiceAuthOptions>(opts =>
{
    var section = builder.Configuration.GetSection("ServiceAuth");
    opts.JwtSecret = section["JwtSecret"] ?? throw new InvalidOperationException("ServiceAuth:JwtSecret is not configured.");
    opts.Issuer = section["Issuer"] ?? throw new InvalidOperationException("ServiceAuth:Issuer is not configured.");
    opts.Audience = section["Audience"] ?? throw new InvalidOperationException("ServiceAuth:Audience is not configured.");
    if (int.TryParse(section["TokenLifetimeSeconds"], out var ttl)) opts.TokenLifetimeSeconds = ttl;
});
builder.Services.AddSingleton<ServiceTokenIssuer>();
builder.Services.AddTransient<ServiceAuthHandler>();
builder.Services.AddHttpClient<INotificationsApi, NotificationsApiClient>(client =>
{
    var baseUrl = builder.Configuration["NotificationsApi:BaseUrl"]
                  ?? throw new InvalidOperationException("NotificationsApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<ServiceAuthHandler>();

builder.Services.AddHttpClient<IPresenceApi, PresenceApiClient>(client =>
{
    var baseUrl = builder.Configuration["PresenceApi:BaseUrl"]
                  ?? throw new InvalidOperationException("PresenceApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<ServiceAuthHandler>();

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
    ISessionService sessionService,
    IMediator mediator,
    HttpContext httpContext) =>
{
    var userId = await sessionService.GetUserIdAsync(token);
    if (userId is null) return Results.Redirect("/login?error=invalid");

    var user = await mediator.Send(new GetUserByIdQuery(userId.Value));
    if (user is null) return Results.Redirect("/login?error=invalid");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.Value.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Email, user.Email),
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
