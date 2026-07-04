---
name: client-sdk-plan
description: НАСТУПНА велика задача — TelegramLike.Client SDK (NuGet) + MAUI mobile/desktop апка; k8s відкладено, дефолтний деплой знову compose
metadata:
  node_type: memory
  type: project
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

**Апки: .NET MAUI** — mobile (Android/iOS) і desktop (Windows/macOS) з однієї кодової бази. Розглянути **MAUI Blazor Hybrid** — реюз наявних Razor-компонентів з Web UI (один SDK, спільні компоненти, три платформи). Порядок: SDK (`dotnet pack`) → SignalR Hub → MAUI Hybrid desktop (швидший цикл) → Android.

Див. [[realtime-blazor-pubsub]], [[api-gateway]], [[service-auth-jwt]], [[kubernetes-plan]], [[microservices-migration]].
