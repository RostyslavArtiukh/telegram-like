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

**Not done / possible next:** YARP edge gateway; making sends idempotent (client message-id / Idempotency-Key) to allow retrying POST safely; observability of breaker state. See [[microservices-migration]], [[ci-pipeline]].
