# Web BFF (Blazor Server, port 8080)

Pure BFF: **no domain, no database, no MediatR handlers.** References `TelegramLike.Contracts` + the `TelegramLike.Client` SDK (`src/client/`), which owns all typed HTTP clients. Hosts a MassTransit bus solely so real-time pubsub consumers can push into the Blazor circuit.

## Calling services
- Typed clients (`IChatsApi`, `IMessagingApi`, `IIdentityAuthApi`/`IIdentityUsersApi`, `INotificationsApi`, `IPresenceApi`) live in the **`TelegramLike.Client` SDK** (namespaces `TelegramLike.Client.<Context>`), registered via `AddTelegramLikeApiClients(gatewayUri)` in `Program.cs`.
- **Auth:** the Web registers the scoped `ServiceTokenProvider` (`Services/ServiceAuth/`) as the SDK's `IAccessTokenProvider`; clients resolve it per request and attach `Bearer` themselves. **Never inject scoped auth-state into a `DelegatingHandler`** — handlers are pooled outside the circuit scope and would leak one user's token to another. That's why the provider lives in the clients, not a handler.
- BFF enrichment: compute recipients / isBroadcast / isModerator / isPremium here (from Chats data or the cookie) and pass them into Messaging/Chats calls — services don't cross-query.

## Real-time (no SignalR Hub)
RabbitMQ integration event → Web `IConsumer` → in-memory `IXPubSub` → Razor component (`Subscribe` on init, `InvokeAsync(StateHasChanged)`, unsubscribe on dispose). One pubsub per UI action, not per event type. See the `realtime_blazor_pubsub` memory.

## Render-mode gotcha
Layout and `Routes` render as static SSR; only components with `@rendermode InteractiveServer` are live. Anything needing a persistent circuit (timers, heartbeat) must live in an interactive component — otherwise it disposes once the HTTP response is sent (see `PresenceHeartbeat`, which keeps presence alive).

## Auth flow
`/login` → `IIdentityAuthApi.LoginAsync` → session token → navigate to `/auth/signin?token=` → Web exchanges it at Identity → sets cookie (claims incl. `session_token`, read by `CurrentUserAccessor.GetSessionTokenAsync`). DataProtection keys persist to a volume so cookies survive restarts.
