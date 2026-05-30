using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Application.Identity.Commands.RegisterUser;
using TelegramLike.Application.Identity.Queries.GetUserById;
using TelegramLike.Infrastructure;
using TelegramLike.Web.Components;
using TelegramLike.Web.Services;
using TelegramLike.Web.Services.NotificationsApi;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddTransient<UserIdHeaderHandler>();
builder.Services.AddHttpClient<INotificationsApi, NotificationsApiClient>(client =>
{
    var baseUrl = builder.Configuration["NotificationsApi:BaseUrl"]
                  ?? throw new InvalidOperationException("NotificationsApi:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<UserIdHeaderHandler>();

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
