# Web BFF (Blazor Server, port 8080)

Pure BFF: **no domain, no database, no MediatR handlers.** References `TelegramLike.Contracts` + the `TelegramLike.Client` SDK (`src/client/`), which owns all typed HTTP clients. Hosts a MassTransit bus solely so real-time pubsub consumers can push into the Blazor circuit.

Health ([TL-99]): `/health/live` + `/health/ready` — readiness is the MassTransit bus check only; the gateway is deliberately **not** probed (downstream outage is absorbed by the SDK resilience pipeline + graceful degradation, and gating readiness on it would pull web out of the LB exactly when it can still serve).

## Calling services
- Typed clients (`ChatsApiClient`, `MessagingApiClient`, `IIdentityAuthApi`+`IdentityUsersApiClient`, `NotificationsApiClient`, `PresenceApiClient` — конкретні класи; інтерфейс лишився тільки в auth-клієнта, бо його мокають тести) live in the **`TelegramLike.Client` SDK** (namespaces `TelegramLike.Client.<Context>`), registered via `AddTelegramLikeApiClients(gatewayUri)` in `Program.cs`.
- **Auth:** the Web registers the scoped `ServiceTokenProvider` (`Services/ServiceAuth/`) as the SDK's `IAccessTokenProvider`; clients resolve it per request and attach `Bearer` themselves. **Never inject scoped auth-state into a `DelegatingHandler`** — handlers are pooled outside the circuit scope and would leak one user's token to another. That's why the provider lives in the clients, not a handler.
- BFF enrichment: compute isBroadcast / isModerator here (from Chats data or the cookie) and pass them into Messaging/Chats calls — services don't cross-query. **Recipients are no longer enriched here** ([TL-118]) and `isPremium` never was ([TL-102]): Messaging derives both itself. Before adding a new enriched field, check the service can't already derive it — a second copy of a cross-context derivation is the thing this list keeps shrinking.

## Real-time (no SignalR Hub)
RabbitMQ integration event → Web `IConsumer` → in-memory `XPubSub` (конкретні класи, без інтерфейсів) → Razor component (`Subscribe` on init, `InvokeAsync(StateHasChanged)`, unsubscribe on dispose). One pubsub per UI action, not per event type. See the `realtime_blazor_pubsub` memory.

All five pubsubs are thin wrappers over one `CircuitTopics<TCallback>` ([TL-126]) — they were five copies of the same registry, each of which **never released a topic**, so a replica accumulated an entry per chat ever opened and per user ever rendered and only ever grew. A topic is now dropped with its last subscriber, under that topic's lock so a subscribe racing the removal can't attach to a detached dictionary (a silently dead subscription = real-time stops for that chat with nothing logged). Covered by `TelegramLike.Web.Tests`.

## This host is stateful — that's the scaling shape
A circuit holds its tab's component state on **one** instance, so memory follows open tabs (not request rate) and the tier needs **sticky sessions**. The in-memory pubsub is correct precisely because of it: every replica has its own RabbitMQ queue (`Temporary = true` + `InstanceId`) and pushes only to its own circuits. `Program.cs` states the memory ceiling explicitly instead of inheriting framework defaults — `Circuits:DisconnectedCircuitMaxRetained` / `DisconnectedCircuitRetentionMinutes` (the defaults hold 100 whole circuits for 3 min after the browser is gone) and `Circuits:MaxBufferedUnacknowledgedRenderBatches` (per-tab, so it multiplies).

## Render-mode gotcha
Layout and `Routes` render as static SSR; only components with `@rendermode InteractiveServer` are live. Anything needing a persistent circuit (timers, heartbeat) must live in an interactive component — otherwise it disposes once the HTTP response is sent (see `PresenceHeartbeat`, which keeps presence alive).

## Auth flow
`/login` posts credentials via a native `<form method="post">` (with `<AntiforgeryToken />`) to the `/auth/signin` action in `Controllers/AuthController.cs` (moved out of Program.cs in [TL-90] — no inline endpoints), which does it all server-side in one request: `LoginAsync` (credentials → session token) → `ExchangeAsync` at Identity (→ identity claims) → sets the cookie (claims incl. `session_token`, read by `CurrentUserAccessor.GetSessionTokenAsync`). The session token is minted and consumed on the server and **never reaches the browser** (no query string, no hidden field). Antiforgery is validated manually so a stale/forged post redirects to `/login?error=…` instead of a raw 400. DataProtection keys persist to a volume so cookies survive restarts.

**Logout** (`/auth/signout`, a real `<form>` post from `NavMenu`): revokes the session server-side (`identity.LogoutAsync` → Identity deletes the Redis key, best-effort so a downstream failure never blocks sign-out) **then** `SignOutAsync` drops the cookie and redirects to `/login`. Route gating already exists: protected pages carry `[Authorize]` and `Routes.razor`'s `AuthorizeRouteView` → `RedirectToLogin` bounces any unauthenticated navigation to `/login` (public pages: `login`/`register`/`Error`).
