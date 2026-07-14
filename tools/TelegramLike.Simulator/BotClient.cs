using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Client;
using TelegramLike.Client.Auth;
using TelegramLike.Client.Chats;
using TelegramLike.Client.Messaging;
using TelegramLike.Client.Presence;
using TelegramLike.Client.Realtime;

namespace TelegramLike.Simulator;

/// <summary>
/// Один бот = один повноцінний SDK-клієнт зі своїм DI-контейнером.
/// <c>AddTelegramLikeClient</c> розрахований на «одна користувацька сесія на процес»
/// (singleton <see cref="TelegramLikeSession"/>), а нам потрібен десяток незалежних
/// сесій в одному процесі — тому контейнер на бота, як десять маленьких MAUI-застосунків.
/// </summary>
public sealed class BotClient : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public string Username { get; }
    public string DisplayName { get; }
    public string Email { get; }

    public TelegramLikeSession Session { get; }
    public ChatsApiClient Chats { get; }
    public MessagingApiClient Messaging { get; }
    public PresenceApiClient Presence { get; }
    public TelegramLikeRealtimeClient Realtime { get; }

    public Guid UserId => Session.UserId
        ?? throw new InvalidOperationException($"Бот {Username} ще не залогінений.");

    public BotClient(Uri gatewayBaseUrl, string username, string displayName, string email)
    {
        Username = username;
        DisplayName = displayName;
        Email = email;

        var services = new ServiceCollection();
        services.AddTelegramLikeClient(gatewayBaseUrl);
        _provider = services.BuildServiceProvider();

        Session = _provider.GetRequiredService<TelegramLikeSession>();
        Chats = _provider.GetRequiredService<ChatsApiClient>();
        Messaging = _provider.GetRequiredService<MessagingApiClient>();
        Presence = _provider.GetRequiredService<PresenceApiClient>();
        Realtime = _provider.GetRequiredService<TelegramLikeRealtimeClient>();
    }

    /// <summary>Логін наявним акаунтом; якщо бота ще нема — реєстрація + логін. Повертає true, коли реєстрував.</summary>
    public async Task<bool> LoginOrRegisterAsync(string password, CancellationToken cancellationToken)
    {
        try
        {
            await Session.LoginAsync(Email, password, cancellationToken);
            return false;
        }
        catch (InvalidOperationException)
        {
            // 400 від Identity — акаунта ще нема (або змінили пароль у конфізі;
            // тоді реєстрація впаде з "already taken" і чесно про це скаже).
            await Session.RegisterAsync(Email, Username, DisplayName, password, cancellationToken);
            await Session.LoginAsync(Email, password, cancellationToken);
            return true;
        }
    }

    public async ValueTask DisposeAsync()
        => await _provider.DisposeAsync(); // контейнер володіє і Realtime-клієнтом, і HTTP-фабрикою
}
