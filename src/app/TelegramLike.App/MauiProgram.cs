using Microsoft.Extensions.Logging;
using TelegramLike.App.Services;
using TelegramLike.Client;

namespace TelegramLike.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Everything the app needs to talk to the backend comes from the SDK:
		// typed HTTP clients through the gateway, TelegramLikeSession (login →
		// cached JWT), and the SignalR realtime client.
		builder.Services.AddTelegramLikeClient(new Uri(AppConfig.GatewayBaseUrl));
		builder.Services.AddSingleton<UsernameCache>();
		builder.Services.AddSingleton<PresenceHeartbeat>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

public static class AppConfig
{
	// Windows desktop talks to the compose stack on the same machine. On Android
	// this must become http://<PC-LAN-IP>:8090 (plus cleartext-HTTP config) —
	// handled when the android target is added.
	public const string GatewayBaseUrl = "http://localhost:8090";
}
