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
	// Android emulator (default target): 10.0.2.2 is the emulator's magic alias
	// for the host machine's loopback, so it reaches the compose stack on the PC
	// with no firewall rule and no LAN address. Plain HTTP → android:usesCleartextTraffic
	// in the manifest.
	//
	// For a *physical* device instead, swap this for the PC's LAN address
	// (e.g. "http://192.168.0.101:18090") — both must share a Wi-Fi network and
	// Windows Firewall must allow 18090 inbound.
	public const string GatewayBaseUrl = "http://10.0.2.2:18090";
#else
	// Windows desktop talks to the compose stack on the same machine.
	public const string GatewayBaseUrl = "http://localhost:18090";
#endif
}
