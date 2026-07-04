---
name: bff-resilience
description: BFF HTTP resilience — timeout/retry/circuit-breaker on all downstream service clients
metadata:
  node_type: memory
  type: project
---

**[TL-54/TL-55] (2026-07-04):** The Web BFF's five typed clients (identity, notifications, presence, chats, messaging) all go through one shared resilience policy — `Services/Resilience/ResilientHttpClientExtensions.cs` `AddServiceResilience()`, chained onto each `AddHttpClient<>` in `Program.cs`.

**Package:** `Microsoft.Extensions.Http.Resilience` **9.10.0** — pinned to the 9.x line on purpose. `dotnet add` grabbed 10.7.0 because the dev box has SDK 10, but the app runs on the net9 runtime, so use the 9.x package (CI uses the net9 SDK too).

**Policy (standard resilience handler, tuned):**
- AttemptTimeout 5s, TotalRequestTimeout 20s.
- Retry: `DisableForUnsafeHttpMethods()` so **POST/PATCH are never retried** — a lost response must not double-send a message / double-create a chat / double-register. MaxRetryAttempts 3, base Delay **200ms** (not the 2s default: intra-cluster hops feeding an interactive UI must detect a down service in ~1s and keep failures inside the breaker's sampling window).
- CircuitBreaker: MinimumThroughput 5 (dropped from default 100 for low local traffic so it actually trips), SamplingDuration 30s (must stay >= 2*AttemptTimeout), FailureRatio 0.5, BreakDuration 10s.

**Read-path degradation (TL-55, read-paths only per user):** most read widgets already `catch`-swallow (unread badge, presence dots, typing — a down service just renders nothing). The real gap was `Notifications.razor` showing "Loading…" forever on first-load failure → now a quiet "temporarily unavailable" state (`_loadFailed` flag), plus a small stale banner when a refresh fails but a prior feed exists. Write actions still surface real errors.

**Verified live:** compose up → `docker compose stop notifications` → web still served (302 in 5ms), other services healthy (failure isolation). Reproduced the exact pipeline against the down port: breaker opens after ~1.5 calls then fast-fails in 0ms; POST fast-fails when open. Caveat: first hit on a *hung* host (Docker drops SYN on a stopped container) costs up to TotalRequestTimeout before the breaker learns — inherent to detecting a hang.

**[TL-59] idempotent sends (done):** `SendMessage` is now idempotent — the BFF generates the message id client-side, sends it in the body + an `Idempotency-Key` header; Messaging uses it as the aggregate `_id`, and `MessageRepository.AddAsync` catches a duplicate `_id` (E11000) as success (txn aborts → no re-insert, no re-queued `MessageSent`). So the resilience retry was **re-opened for keyed sends**: the retry predicate now replaces `DisableForUnsafeHttpMethods()` with `ShouldHandle = retry idempotent methods OR any request carrying an Idempotency-Key` (plain POST/PATCH still not retried). Predicate reads the request via `args.Context.GetRequestMessage()`; uses stable `HttpClientResiliencePredicates.IsTransient(args.Outcome)` (the CT overload is `[Experimental]` EXTEXP0001). Verified: 3 same-id sends → 1 message; GET/keyed-POST retried, keyless-POST not. Pattern is reusable for other non-idempotent writes (chat create, register).

**[TL-60] idempotency extended to chat-create + register** — same pattern: client-generated aggregate id = idempotency key, sent in body + `Idempotency-Key` header. Chat create (direct/group/broadcast): `ChatRepository.AddAsync` catches duplicate `_id` (direct still returns the server id since pair-lookup may resolve to an existing chat). Register: handler does a `GetById(userId)` up front and returns idempotently **before** the email/username "taken" checks (so a user's own retry doesn't trip them; a real duplicate-email with a new id still errors). Verified: 3 same-id group creates → 1 chat; 2 same-id+email registers → same id, 200 both. **Gotcha when curl-testing chats through the gateway:** the chats prefix equals its route prefix, so the real BFF path is doubled (`/chats/chats/group`), not `/chats/group` (that strips to `/group` → 404). See [[api-gateway]].

**Reusable idempotency recipe (messaging TL-59, chats+identity TL-60):** (1) aggregate factory takes `Guid id`; (2) command/request carry the id, empty→mint; (3) repo `AddAsync` catches duplicate `_id` E11000 as success (txn aborts → no re-insert, no re-queued events) — or for identity (no txn/outbox) the handler does a `GetById` pre-check; (4) BFF client generates id + sets `Idempotency-Key` header + returns it; (5) resilience predicate already retries any `Idempotency-Key` request.

**Not done / possible next:** observability of breaker state (partly covered — Polly meter now scraped, see [[observability-metrics]]); alerting rules on Prometheus metrics. See [[microservices-migration]], [[ci-pipeline]], [[api-gateway]], [[observability-metrics]].

**[TL-56] gateway added:** the five per-service `*Api:BaseUrl` settings are gone — all clients now use one `Gateway:BaseUrl` + a `ServicePrefixHandler` (inner to the resilience handler). See [[api-gateway]].
