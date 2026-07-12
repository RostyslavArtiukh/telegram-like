# TelegramLike.App — MAUI Blazor Hybrid client

Native desktop/mobile messenger app: MAUI shell + `BlazorWebView` rendering Razor components. All backend access goes through the **`TelegramLike.Client` SDK** (`AddTelegramLikeClient(gatewayUrl)` in `MauiProgram`): typed HTTP clients, `TelegramLikeSession` auth, SignalR realtime client. No cookies, no in-proc pubsub — this is the SDK's reference consumer.

## Build & run (Windows)
- **NOT in `TelegramLike.sln`** — CI builds that on ubuntu and a Windows-TFM MAUI project would break it. Use `TelegramLike.App.slnx` (app + Client + Contracts) or build the csproj directly.
- Requires the `maui-windows` workload (SDK 10; app targets `net10.0-windows10.0.19041.0`, referencing the net9.0 SDK/Contracts is fine). Android TFM is added only when the android workload/SDK step happens; iOS excluded (no Mac).
- `dotnet build src/app/TelegramLike.App/TelegramLike.App.csproj -f net10.0-windows10.0.19041.0` → run `bin/.../win-x64/TelegramLike.App.exe` (unpackaged, `WindowsPackageType=None`).
- Backend must be up (`docker compose up -d`); gateway URL is `AppConfig.GatewayBaseUrl` (localhost:18090 on Windows; Android will need the PC's LAN IP + cleartext-HTTP config).

## Patterns
- **Realtime lifecycle:** connect once after login (`Login.EnterAsync`), `JoinChatAsync`/`LeaveChatAsync` per open chat (Chat.razor), disconnect on sign-out. Hub events fire on background threads → always `InvokeAsync(StateHasChanged)`.
- Pushes carry ids only; components refetch the entity over HTTP (`GetMessageByIdAsync`) — same signal-then-fetch model as the events themselves.
- Enrichment (recipients/isBroadcast) happens app-side via `ChatsApiClient` helpers before `SendMessageAsync` — mirrors the Web BFF until enrichment moves server-side.
- `PresenceHeartbeat` singleton: 20s heartbeat while signed in (Redis TTL 30s); `UsernameCache` batches id→username lookups.
- Session store is in-memory for now — login is per-launch. Android step should register a SecureStorage-backed `ISessionStore`.

## E2E driving (for verification)
WebView2 honors `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333` — set it, start the exe, then Playwright `connectOverCDP('http://localhost:9333')` drives the app's Blazor UI like a page. See the root verify skill.
