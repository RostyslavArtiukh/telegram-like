---
name: client-sdk-plan
description: "SDK [TL-64] + realtime hub [TL-65] + MAUI desktop [TL-66..68] + Android-prep [TL-67] — ЗРОБЛЕНО й запушено; лишились фізичні кроки Android (SDK/adb/firewall/телефон); деплой compose"
metadata: 
  node_type: memory
  type: project
  originSessionId: f9c2fad8-770d-4336-bac7-c7020e5b76d9
---

**Рішення (2026-07-04, вечір):** наступна велика задача — **клієнтський SDK як NuGet-пакет + мобільна/десктоп апка** на ньому. Юзер сформулював як «загорнути солюшен у NuGet» — уточнили: пакується НЕ бекенд, а **`TelegramLike.Client` SDK** (Contracts + typed API clients + auth flow + real-time клієнт). Бекенд лишається запущеною інфрою, апки ходять до нього по HTTP через gateway.

**K8s відкладено:** дефолтний деплой для розробки — знову `docker compose` (стек піднімати compose'ом). K8s-маніфести ([TL-62]/[TL-63]) лишаються в репо, робочі й verified — не чіпати без потреби. K8s-кластер у Docker Desktop можна вимкнути для економії ресурсів.

**База вже є:**
- `TelegramLike.Contracts` — окремий проєкт, готовий кандидат у пакет.
- Typed clients у Web BFF (`Services/<Name>Api/`: `IChatsApi`, `IMessagingApi`, `IIdentityAuthApi`…) — переїжджають у SDK майже без змін (разом із `ServicePrefixHandler` + resilience).
- Auth-флоу для не-браузерних клієнтів уже існує: `POST /auth/login` → session token, `POST /auth/token` → короткоживучий JWT. Кукі не потрібні.

**Дві архітектурні дірки, які треба закрити:**
1. **Real-time для зовнішніх клієнтів** — поточний шлях (RabbitMQ → in-proc pubsub → Blazor circuit) живе тільки у Web-процесі. Потрібен **SignalR Hub** (консюмить ті самі integration events; [[realtime-blazor-pubsub]] це передбачала: «Hub буде потрібен якщо додамо mobile app»). Питання: хостити Hub у Web чи окремим realtime-сервісом.
2. **BFF-enrichment** (recipients, isBroadcast, isPremium) — зараз у Web BFF; мобільна апка напряму в gateway мусила б дублювати. Винести у SDK або (краще) у спільний server-side шар — заодно закриє відомий fail-open у messaging (membership не валідується).

**Апки: .NET MAUI Blazor Hybrid** (обрано 2026-07-04; Blazor Hybrid = режим UI всередині MAUI: BlazorWebView + Razor-компоненти, C# нативно). Порядок: SDK (`dotnet pack`) → SignalR Hub → MAUI Hybrid desktop (швидший цикл) → Android-емулятор.

**Платформи (рішення 2026-07-04): iOS викреслено свідомо** — основний телефон юзера iPhone, але Mac'а нема, а без нього деплой на iOS неможливий (Xcode-підпис). **Цілі: Windows desktop + фізичний Android-телефон юзера** (запасний, є в наявності) — деплой напряму з Visual Studio по USB (одноразово ввімкнути Developer options + USB debugging на телефоні; апка і бекенд на ПК мають бути в одній Wi-Fi, SDK BaseUrl = `http://<IP ПК>:8090` gateway — врахувати Android cleartext-HTTP policy: або `android:usesCleartextTraffic`, або network security config для dev). Емулятор — лише fallback.

**Каверза реюзу Razor-компонентів:** не copy-paste — поточні компоненти зав'язані на Blazor Server-патерни (typed clients з DI Web-процесу, in-proc pubsub). У Hybrid дані мають іти через SDK (HTTP + SignalR) → компоненти треба відв'язати на спільні інтерфейси (shared UI library).

**Прогрес [TL-64] (2026-07-05): Фаза 1 — SDK — ЗРОБЛЕНО й live-verified.**
- `src/client/TelegramLike.Client/` (net9.0, packable, `dotnet pack` → `artifacts/packages/`): всі 6 typed clients переїхали з Web (namespaces `TelegramLike.Client.<Context>`), `Http/` (ServicePrefixHandler + resilience), `Auth/` — нова абстракція **`IAccessTokenProvider`** (клієнти беруть токен через неї), `ISessionStore` + `TelegramLikeSession` (standalone login→exchange→cache, для MAUI/console; сесія singleton на процес).
- DI: `AddTelegramLikeApiClients(uri)` (хост дає свій provider) vs `AddTelegramLikeClient(uri)` (standalone + session). Web тепер референсить SDK; його `ServiceTokenProvider` імплементує `IAccessTokenProvider` (scoped, cookie-based). Дублікати у `Web/Services/*Api|Resilience` видалені (~900 рядків).
- **Гочі:** Web Dockerfile restore-stage мусить COPY новий csproj (`src/client/...`), інакше образ не збирається. Playwright-верифікація повного флоу записана у проектний skill `.claude/skills/verify/SKILL.md` (Blazor prerender-trap, hidden inputs, navigate-on-create).
- **Далі:** SignalR Hub для зовнішніх клієнтів (дірка №1) → MAUI Blazor Hybrid desktop → Android. Дірка №2 (enrichment) поки НЕ закрита server-side: `IChatsApi` enrichment-хелпери переїхали в SDK, standalone-апка робитиме enrichment клієнтом SDK (fail-open у messaging: частково закрито [TL-70] — SendMessage гібридний fail-closed; `AddReaction`/`MarkAsRead` та spoofable `isBroadcast`/`isPremium` досі відкриті — кандидат на окремий [TL-N]).

**Прогрес [TL-65] (2026-07-05): Фаза 2 — SignalR Hub — ЗРОБЛЕНО й live-verified.** Дірка №1 закрита.
- Рішення hub-hosting: **окремий одно-проектний сервіс** `src/services/realtime/TelegramLike.Realtime.Api` (порт 8086, без БД/домену), НЕ у Web — бо Web cookie-authed, а hub'у треба JWT; і SDK лишається на одному gateway base URL. Hub `/hub` → через gateway `/realtime/hub` (YARP проксує WebSocket з коробки).
- Групи: `user:{sub}` auto-join на connect (сирий `sub` claim!), `chat:{chatId}` через `JoinChat`/`LeaveChat`. Спліт подій проти подвійної доставки: `MessageSent`→chat-група, `ChatActivity`→user-групи (recipients+author). Per-instance temporary черги як у Web [TL-63]. Payload-шейпи + імена у `Contracts/Realtime/RealtimeEvents.cs` (спільні з SDK).
- SDK: `ITelegramLikeRealtimeClient` (SignalR client, auto-reconnect + re-join chat-груп, `AccessTokenProvider` → session). Реєструється в `AddTelegramLikeClient`.
- Verified консольним SDK-клієнтом через gateway: anon connect → 401, typing/message/chat-activity/reaction пуші приходять, LeaveChat зупиняє. Рецепт у verify skill.
- **Наступний крок: MAUI Blazor Hybrid desktop апка** (потім Android по USB).

**Прогрес [TL-66] (2026-07-05): Фаза 3 — MAUI desktop апка — ЗРОБЛЕНО й live-verified.**
- `src/app/TelegramLike.App` — maui-blazor template, **тільки Windows TFM** (`net10.0-windows10.0.19041.0`; workload `maui-windows` встановлено; глобальний SDK на машині вже .NET 10, апка net10 референсить net9 SDK-ліби без проблем). **НЕ в TelegramLike.sln** (CI = ubuntu, зламалось би) — окремий `TelegramLike.App.slnx`.
- Сторінки: Login/Register/Chats/Chat — усе через SDK (`AddTelegramLikeClient`), enrichment app-side через `IChatsApi`-хелпери. Патерн: hub-пуш (id-only) → InvokeAsync → refetch по HTTP. `PresenceHeartbeat` (20s) + `UsernameCache` синглтони.
- **Верифікація — CDP-трюк:** WebView2 поважає `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333` → Playwright `connectOverCDP` водить НАТИВНУ апку. Cross-client тест: web-юзер у браузері відповідає → повідомлення/typing/presence з'являються у нативній апці live. ALL PASS.
- **Далі (наступна сесія): Android** — встановити android workload + Android SDK, повернути android TFM, `AppConfig` → LAN IP ПК + cleartext-HTTP, SecureStorage-based `ISessionStore`, деплой по USB з фізичним телефоном.

**Android-крок ([TL-67]) — код ЗАКОМІЧЕНО Й ЗАПУШЕНО (перевірено 2026-07-13, `git log`/`status`); лишилась тільки фізична частина:**
- У репо вже є: android TFM у csproj, `usesCleartextTraffic` у AndroidManifest, `AppConfig` → `#if ANDROID` → `http://192.168.0.101:18090` (LAN IP ПК — перевірити при резюмі, DHCP; порт уже **18090** після переїзду compose host-портів на 18xxx), `SecureSessionStore` (SecureStorage, реєструється `#if ANDROID` ПЕРЕД `AddTelegramLikeClient` у `MauiProgram`). `maui-android` workload встановлено.
- **Лишилось (потрібні дії юзера):** (1) Android SDK/JDK/adb — `dotnet build -f net10.0-android -t:InstallAndroidDependencies -p:AcceptAndroidSDKLicenses=true`; (2) firewall inbound **18090** elevated: `New-NetFirewallRule -DisplayName "TelegramLike gateway 18090" -Direction Inbound -Protocol TCP -LocalPort 18090 -Action Allow -Profile Private` (або перевірити, чи Docker Desktop правила вже пропускають); (3) телефон: Developer options + USB debugging + той самий Wi-Fi; (4) деплой по USB.
- [TL-66] (MAUI desktop) — на origin разом з усім іншим.

Див. [[realtime-blazor-pubsub]], [[api-gateway]], [[service-auth-jwt]], [[kubernetes-plan]], [[microservices-migration]].
