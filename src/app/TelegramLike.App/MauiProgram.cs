using Microsoft.Extensions.Logging;
using MudBlazor.Services;
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

		// MudBlazor: theme, dialogs, snackbars, popovers for the Blazor Hybrid UI.
		builder.Services.AddMudServices();

		// Everything the app needs to talk to the backend comes from the SDK:
		// typed HTTP clients through the gateway, TelegramLikeSession (login →
		// cached JWT), and the SignalR realtime client.
#if ANDROID
		// Must precede AddTelegramLikeClient — the SDK TryAdds an in-memory store.
		builder.Services.AddSingleton<TelegramLike.Client.Auth.ISessionStore, SecureSessionStore>();
#endif
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
#if ANDROID
	// The phone reaches the compose stack over Wi-Fi via the PC's LAN address
	// (both must be on the same network; Windows Firewall must allow 18090 in).
	// Plain HTTP → android:usesCleartextTraffic in the manifest. Update the IP
	// if the PC's DHCP lease changes.
	public const string GatewayBaseUrl = "http://192.168.0.101:18090";
#else
	// Windows desktop talks to the compose stack on the same machine.
	public const string GatewayBaseUrl = "http://localhost:18090";
#endif
}
