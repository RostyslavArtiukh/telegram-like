# Web BFF (Blazor Server, port 8080)

Pure BFF: **no domain, no database, no MediatR handlers.** References only `TelegramLike.Contracts` + typed HTTP clients to the 5 services. Hosts a MassTransit bus solely so real-time pubsub consumers can push into the Blazor circuit.

## Calling services
- One typed client per service in `Services/<Name>Api/` (`I<Name>Api` + `<Name>ApiClient`).
- **Auth:** each client resolves an access token via the scoped `ServiceTokenProvider` (`Services/ServiceAuth/`) and attaches `Bearer` itself. **Never inject scoped auth-state into a `DelegatingHandler`** — handlers are pooled outside the circuit scope and would leak one user's token to another. That's why the provider lives in the clients, not a handler.
- `Services/IdentityApi/`: `IIdentityAuthApi` (public — register/login/exchange, plain client, no token) vs `IIdentityUsersApi` (authed user queries).
- BFF enrichment: compute recipients / isBroadcast / isModerator / isPremium here (from Chats data or the cookie) and pass them into Messaging/Chats calls — services don't cross-query.

## Real-time (no SignalR Hub)
RabbitMQ integration event → Web `IConsumer` → in-memory `IXPubSub` → Razor component (`Subscribe` on init, `InvokeAsync(StateHasChanged)`, unsubscribe on dispose). One pubsub per UI action, not per event type. See the `realtime_blazor_pubsub` memory.

## Render-mode gotcha
Layout and `Routes` render as static SSR; only components with `@rendermode InteractiveServer` are live. Anything needing a persistent circuit (timers, heartbeat) must live in an interactive component — otherwise it disposes once the HTTP response is sent (see `PresenceHeartbeat`, which keeps presence alive).

## Auth flow
`/login` → `IIdentityAuthApi.LoginAsync` → session token → navigate to `/auth/signin?token=` → Web exchanges it at Identity → sets cookie (claims incl. `session_token`, read by `CurrentUserAccessor.GetSessionTokenAsync`). DataProtection keys persist to a volume so cookies survive restarts.
