# TelegramLike.Client — the client SDK (NuGet)

Typed .NET SDK every client app uses to talk to the backend **through the YARP gateway** (one base URL, e.g. `http://localhost:8090`). Consumed by the Web BFF (project reference) and by future MAUI/console apps (project reference or `dotnet pack` → NuGet). References only `TelegramLike.Contracts` + `Microsoft.Extensions.Http.Resilience` — no domain, no ASP.NET Core.

## Layout
- `Identity/ Chats/ Messaging/ Notifications/ Presence/` — one typed client per service (public `<Name>ApiClient`; лише `IIdentityAuthApi` зберіг інтерфейс — його мокають тести `TelegramLikeSession`) with its wire DTOs (`*Contract` records; Notifications DTOs live in Contracts).
- `Http/` — `ServicePrefixHandler` (prepends `/chats` etc., gateway strips it) + shared resilience pipeline (timeout/retry/circuit-breaker; POSTs retried only with `Idempotency-Key`).
- `Auth/` — `IAccessTokenProvider` (per-request JWT resolution), `ISessionStore` (where the opaque session token persists), `TelegramLikeSession` (standalone login/exchange/caching, implements the provider).
- `Realtime/` — `TelegramLikeRealtimeClient` over SignalR (`{gateway}/realtime/hub`): connect after login, `JoinChatAsync` per open chat, C# events per push type (shapes/names from `Contracts/Realtime`). Re-joins chat groups on reconnect. Events fire on background threads — UI must marshal.
- `TelegramLikeClientExtensions` — DI entry points.

## Rules
- **Clients attach `Bearer` themselves via `IAccessTokenProvider` — never via a DelegatingHandler** (pooled handlers outlive server-side user scopes and would leak tokens between users).
- Two DI entry points: `AddTelegramLikeApiClients(uri)` (host brings its own `IAccessTokenProvider` — the Web BFF does this with its cookie-based `ServiceTokenProvider`) vs `AddTelegramLikeClient(uri)` (standalone: registers singleton `TelegramLikeSession`; override `ISessionStore` for persistence, e.g. MAUI SecureStorage).
- Paths stay service-relative (`/messages/{id}`); the prefix is the handler's job. Client-generated GUID ids double as `Idempotency-Key` values so creates are retry-safe.
- This package is public surface for apps: keep it free of server-only concerns (MassTransit, cookies, Blazor).
