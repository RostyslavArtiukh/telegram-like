---
name: client-sdk-plan
description: "НАСТУПНА велика задача — TelegramLike.Client SDK (NuGet) + MAUI mobile/desktop апка; k8s відкладено, дефолтний деплой знову compose"
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
- **Далі:** SignalR Hub для зовнішніх клієнтів (дірка №1) → MAUI Blazor Hybrid desktop → Android. Дірка №2 (enrichment) поки НЕ закрита server-side: `IChatsApi` enrichment-хелпери переїхали в SDK, standalone-апка робитиме enrichment клієнтом SDK (fail-open у messaging лишається — кандидат на окремий [TL-N]).

Див. [[realtime-blazor-pubsub]], [[api-gateway]], [[service-auth-jwt]], [[kubernetes-plan]], [[microservices-migration]].
