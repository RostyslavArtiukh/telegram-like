using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TelegramLike.Client.Auth;
using TelegramLike.Client.Chats;
using TelegramLike.Client.Http;
using TelegramLike.Client.Identity;
using TelegramLike.Client.Messaging;
using TelegramLike.Client.Notifications;
using TelegramLike.Client.Presence;

namespace TelegramLike.Client;

public static class TelegramLikeClientExtensions
{
    /// <summary>
    /// Registers the six typed API clients against one gateway base address. Every
    /// client gets the shared resilience pipeline plus its service prefix (which the
    /// YARP gateway strips and routes on).
    ///
    /// The host must also register an <see cref="IAccessTokenProvider"/>: the Web BFF
    /// adapts its cookie-scoped token exchange; standalone apps should prefer
    /// <see cref="AddTelegramLikeClient"/>, which wires <see cref="TelegramLikeSession"/>.
    /// </summary>
    public static IServiceCollection AddTelegramLikeApiClients(this IServiceCollection services, Uri gatewayBaseUrl)
    {
        // ServicePrefixHandler is added AFTER AddServiceResilience so it sits inner to
        // the resilience handler — retries clone the original request, so the prefix is
        // applied once per attempt and never doubled.

        // Public auth client (no token) — also used by IAccessTokenProvider implementations
        // for the session-token exchange.
        AddClient<IIdentityAuthApi, IdentityAuthApiClient>(services, gatewayBaseUrl, "/identity");
        AddClient<IIdentityUsersApi, IdentityUsersApiClient>(services, gatewayBaseUrl, "/identity");
        AddClient<INotificationsApi, NotificationsApiClient>(services, gatewayBaseUrl, "/notifications");
        AddClient<IPresenceApi, PresenceApiClient>(services, gatewayBaseUrl, "/presence");
        AddClient<IChatsApi, ChatsApiClient>(services, gatewayBaseUrl, "/chats");
        AddClient<IMessagingApi, MessagingApiClient>(services, gatewayBaseUrl, "/messaging");

        return services;
    }

    /// <summary>
    /// Full standalone setup for desktop/mobile/console apps: the typed API clients plus
    /// a singleton <see cref="TelegramLikeSession"/> (login → session token → cached JWT)
    /// as the <see cref="IAccessTokenProvider"/>. One user session per process.
    /// Swap the session-token persistence by registering an <see cref="ISessionStore"/>
    /// (e.g. MAUI SecureStorage) before calling this.
    /// </summary>
    public static IServiceCollection AddTelegramLikeClient(this IServiceCollection services, Uri gatewayBaseUrl)
    {
        services.AddTelegramLikeApiClients(gatewayBaseUrl);

        services.TryAddSingleton<ISessionStore, InMemorySessionStore>();
        services.TryAddSingleton<TelegramLikeSession>(sp => new TelegramLikeSession(
            sp.GetRequiredService<IIdentityAuthApi>(),
            sp.GetRequiredService<ISessionStore>()));
        services.TryAddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<TelegramLikeSession>());

        return services;
    }

    private static void AddClient<TInterface, TImplementation>(
        IServiceCollection services, Uri gatewayBaseUrl, string servicePrefix)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(client => client.BaseAddress = gatewayBaseUrl)
            .AddServiceResilience()
            .AddHttpMessageHandler(() => new ServicePrefixHandler(servicePrefix));
    }
}
