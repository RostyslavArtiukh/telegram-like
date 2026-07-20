# TelegramLike.App — MAUI Blazor Hybrid client

Native desktop/mobile messenger app: MAUI shell + `BlazorWebView` rendering Razor components. All backend access goes through the **`TelegramLike.Client` SDK** (`AddTelegramLikeClient(gatewayUrl)` in `MauiProgram`): typed HTTP clients, `TelegramLikeSession` auth, SignalR realtime client. No cookies, no in-proc pubsub — this is the SDK's reference consumer.

## Build & run (Windows)
- **NOT in `TelegramLike.sln`** — CI builds that on ubuntu and a Windows-TFM MAUI project would break it. Use `TelegramLike.App.slnx` (app + Client + Contracts) or build the csproj directly.
- Requires the `maui-windows` workload (SDK 10; app targets `net10.0-windows10.0.19041.0`, referencing the net9.0 SDK/Contracts is fine). The Android TFM (`net10.0-android`) needs the `maui-android` workload + Android SDK/JDK — all installed on this machine (SDK at `%LOCALAPPDATA%\Android\Sdk`, JDK 17 at `C:\Program Files\Java\jdk-17` with `JAVA_HOME` pointing there — the old `JAVA_HOME` was stale, pointing at an uninstalled jdk-20). iOS excluded (no Mac — the iOS *simulator* also needs macOS/Xcode, so iOS can't run on this Windows box at all).
- `dotnet build src/app/TelegramLike.App/TelegramLike.App.csproj -f net10.0-windows10.0.19041.0` → run `bin/.../win-x64/TelegramLike.App.exe` (unpackaged, `WindowsPackageType=None`).
- Backend must be up (`docker compose up -d`); gateway URL is `AppConfig.GatewayBaseUrl` — `localhost:18090` on Windows; on Android (`#if ANDROID`) `http://10.0.2.2:18090`, the emulator's magic alias for the host's loopback (no firewall rule, no LAN IP needed). cleartext-HTTP is allowed in the manifest. For a *physical* device instead, swap in the PC's LAN IP (e.g. `http://192.168.0.101:18090`), share a Wi-Fi network, and open firewall 18090 inbound.

## Android emulator (run & verify on a "phone" without a real device)
The default mobile target is the local **Android emulator** — no physical phone required.
- **AVD:** one named **`telegramlike`** already exists (Android 15 / API 35, `google_apis;x86_64`, pixel_7 profile). Recreate: `avdmanager create avd -n telegramlike -k "system-images;android-35;google_apis;x86_64" -d pixel_7`.
- **Launch:** `%LOCALAPPDATA%\Android\Sdk\emulator\emulator.exe -avd telegramlike -no-boot-anim -gpu auto` (uses WHPX; confirm with `emulator -accel-check`). Wait until `adb shell getprop sys.boot_completed` == `1`.
- **Deploy + run:** `dotnet build src/app/TelegramLike.App/TelegramLike.App.csproj -f net10.0-android -t:Run` — builds, installs and launches on the booted emulator.
- **Drive headlessly** with `adb shell input tap/text` + `adb exec-out screencap -p > shot.png`. Verified end-to-end in-emulator: register → sign in → create group → send message, all reaching the stack via `10.0.2.2:18090` (same MudBlazor UI as web/desktop).
- **sdkmanager gotcha:** on a slow/flaky link `sdkmanager "system-images;…"` restarts the ~1.6 GB download from 0 on every drop and keeps failing ("Failed to download package!"). Fall back to a resumable direct download — `curl -L -C - --retry 999 --retry-all-errors -o img.zip https://dl.google.com/android/repository/sys-img/google_apis/x86_64-35_r09.zip` — then `unzip` into `%LOCALAPPDATA%\Android\Sdk\system-images\android-35\google_apis\` (the zip's top `x86_64/` folder carries `source.properties`, so avdmanager recognises it without sdkmanager registering it).

## Patterns
- **Realtime lifecycle:** connect once after login (`Login.EnterAsync`), `JoinChatAsync`/`LeaveChatAsync` per open chat (Chat.razor), disconnect on sign-out. Hub events fire on background threads → always `InvokeAsync(StateHasChanged)`.
- Pushes carry ids only; components refetch the entity over HTTP (`GetMessageByIdAsync`) — same signal-then-fetch model as the events themselves.
- Enrichment (recipients/isBroadcast) happens app-side via `ChatsApiClient` helpers before `SendMessageAsync` — mirrors the Web BFF until enrichment moves server-side.
- `PresenceHeartbeat` singleton: 20s heartbeat while signed in (Redis TTL 30s); `UsernameCache` batches id→username lookups.
- Session store: in-memory on Windows (login is per-launch); on Android `SecureSessionStore` (SecureStorage-backed `ISessionStore`) is registered `#if ANDROID` **before** `AddTelegramLikeClient` in `MauiProgram` ([TL-67]).

## E2E driving (for verification)
WebView2 honors `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333` — set it, start the exe, then Playwright `connectOverCDP('http://localhost:9333')` drives the app's Blazor UI like a page. See the root verify skill.
