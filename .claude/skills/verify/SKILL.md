---
name: verify
description: Drive the running TelegramLike stack end-to-end in a real browser (register → login → create chat → send message → cross-user realtime) to verify a change works at the UI surface.
---

# Verify TelegramLike live

## Build + launch
```powershell
# Web source changed? no-cache the web image (publish-layer cache gotcha)
docker compose build --no-cache web
docker compose up -d --build
docker compose ps   # wait for (healthy) on mongo/rabbitmq/gateway/5 services
```
Web at http://localhost:18080. If a new project was added under `src/`, the Web
`Dockerfile` restore stage must COPY its csproj too, or the image build fails.

## Drive (Playwright via node, not `playwright test`)
`npm i playwright` in a scratch dir (browsers already in `%LOCALAPPDATA%\ms-playwright`),
then a plain `node script.mjs` with `import { chromium } from 'playwright'`.

Flow that exercises the whole read+write+realtime path: register user1 →
login → create group chat → send message → register user2 in a second browser
context → join by chat id → assert history visible → user2 sends reply →
assert it appears on user1's still-open page (RabbitMQ → pubsub → circuit).

## Blazor Server gotchas (all cost real debugging time)
- **Prerender trap:** pages look ready before the circuit attaches; clicks then do
  a static form post that silently resets the form. Wait ~2.5s after `waitForURL`
  post-login, and wrap create/join in a fill+click retry loop (3 × 8s).
- **Hidden inputs:** `form input` matches Blazor's `__RequestVerificationToken` /
  `_handler` hidden fields first — always select `input.form-control`.
- **Create group / Join navigate straight into `/chat/{id}`** — assert with
  `waitForURL('**/chat/**')` and take the id from the URL, not from a list link.
- Composer: `getByPlaceholder('Type a message…')` + button `Send`.
- Login error surface: `.alert-danger` (probe: wrong password → "Invalid email or password.").

## MAUI desktop app surface
Build `src/app/TelegramLike.App` (`-f net10.0-windows10.0.19041.0`), then drive the
NATIVE app with Playwright over CDP:
```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS='--remote-debugging-port=9333'
Start-Process <bin>\win-x64\TelegramLike.App.exe
```
then `chromium.connectOverCDP('http://localhost:9333')` → `contexts()[0].pages()[0]`
is the Blazor UI (url `https://0.0.0.1/`). Selectors: `input.form-control`,
placeholders `Group name` / `Type a message…`. Best cross-client check: web browser
user joins the app's chat and replies — the reply/typing/presence must appear in the
app live. Kill with `Stop-Process -Name TelegramLike.App`.

## SDK / realtime surface (external clients)
For SDK or Realtime-service changes, drive the **package boundary** instead: a scratch
console app referencing `src/client/TelegramLike.Client.csproj`, using
`AddTelegramLikeClient(new Uri("http://localhost:18090"))` (the gateway). Flow:
`session.RegisterAsync/LoginAsync` → `TelegramLikeRealtimeClient.ConnectAsync` +
`JoinChatAsync` → act over HTTP (`ChatsApiClient`/`MessagingApiClient`/`PresenceApiClient`) → assert
hub pushes arrive (TaskCompletionSource + timeout). Probes that matter: anonymous hub
connect must 401; after `LeaveChatAsync` chat-group pushes must stop.
