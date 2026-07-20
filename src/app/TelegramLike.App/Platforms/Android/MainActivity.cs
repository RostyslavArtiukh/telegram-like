using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace TelegramLike.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// Android 15 (API 35+) enforces edge-to-edge: the WebView draws behind the
		// now-transparent status/navigation bars, so the app header slid under the
		// clock/wifi/battery icons. Inset the content view by the system bars (plus
		// any display cutout) so the Blazor UI starts below the status bar and above
		// the gesture bar, and use dark status-bar icons for the light background.
		if (Window is not null)
		{
			var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
			insetsController.AppearanceLightStatusBars = true;
		}

		var content = FindViewById(Android.Resource.Id.Content);
		if (content is not null)
		{
			ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsInsetsListener());
			ViewCompat.RequestApplyInsets(content);
		}
	}

	private sealed class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
	{
		public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
		{
			if (v is null || insets is null)
				return insets;

			var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
			v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
			return insets;
		}
	}
}
