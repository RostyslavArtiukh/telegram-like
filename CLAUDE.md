# TelegramLike — project guide

Telegram-like messenger. Migrated from a modular monolith to **microservices + a Blazor Server BFF**. DDD, CQRS (MediatR inside services), event-driven (RabbitMQ/MassTransit + transactional outbox).

## Architecture
- **Web BFF** — `src/TelegramLike.Web`, port 8080. Blazor Server, pure BFF: no domain, no DB. Talks to services **through the gateway** over HTTP (one `Gateway:BaseUrl`, not five service URLs); hosts a MassTransit bus only for real-time pubsub consumers. See its own `CLAUDE.md`.
- **Gateway** — `src/gateway/TelegramLike.Gateway`, port **8090**. YARP reverse proxy: routes `/<service>/**` to each service and strips the prefix (routes generated in `Program.cs` from one `backends` list; only destination addresses stay config/env-overridable). Does no auth — forwards `Authorization` untouched; each service validates the JWT. Needed because chats and messaging both serve `/chats/*`. **Rate-limits per caller** ([TL-128]): token bucket keyed on the bearer token's `sub` (read, not verified — a bucket key, not an authorization decision) falling back to source address, `RateLimiting:*`, health/metrics exempt. See `bff_resilience` + the routing note below.
- **Client SDK** — `src/client/TelegramLike.Client`. NuGet-packable typed clients for all 5 services (via the gateway) + auth flow + SignalR realtime client; consumed by the Web BFF and the MAUI app. See `src/client/CLAUDE.md`.
- **MAUI app** — `src/app/TelegramLike.App`. Blazor Hybrid desktop (Windows now, Android next) built purely on the SDK. **Not in `TelegramLike.sln`** (CI is ubuntu) — use `TelegramLike.App.slnx`. See `src/app/CLAUDE.md`.
- **5 services** — `src/services/<name>/`, each = Domain/Application/Infrastructure/Api, own Mongo DB, own port:
  - identity **8085** · notifications **8081** · presence **8082** · chats **8083** · messaging **8084**
- **Realtime** — `src/services/realtime`, port **8086**. Single-project SignalR hub for external clients (SDK/MAUI): relays integration events into per-user/per-chat hub groups. No DB, no domain. `JoinChat` authorizes by asking Chats **as the connecting user** and caching the answer ([TL-127]) — the one place a service calls another over HTTP, straight to `Chats:BaseUrl` rather than through the gateway (which already waits on realtime). The Web BFF does not use it. See `src/services/realtime/CLAUDE.md`.
- **Shared layer — versioned NuGet packages, not project references** ([TL-120]). `TelegramLike.Contracts` + the four `src/shared/` projects each carry a `PackageId`/`Version` and live in their **own solution, `TelegramLike.Shared.slnx`**, deliberately outside `TelegramLike.sln`: a project reference in the services' solution would silently win over the package version and make the boundary decorative. Services consume them with `PackageReference`, resolved from the local feed `artifacts/packages` (see `NuGet.config`).
  - **Bootstrap before anything else**: `./build/pack-shared.ps1` (also the first CI step, and required before `docker compose build`). It packs in dependency order because `Shared.Application` restores `Shared.Domain` *as a package*, and it evicts our own ids from the NuGet cache first — re-packing a version otherwise leaves consumers on the previous build of it.
  - **Changing shared code = a version bump** in that csproj plus the `PackageReference`s that should move. That friction is the deploy coupling made visible: before this, a one-line edit in `Shared.Infrastructure` silently rebuilt and required redeploying all six apps at once.
- **Shared projects** — `src/shared/`, one per layer so a layer only drags its own dependencies:
  - `TelegramLike.Shared.Domain` (zero deps): `ObjectWithId` (id + equality), `ObjectWithEvents` (records `IChangeEvent`s via `RecordEvent`/`PendingEvents`/`ClearPendingEvents`), `DomainException`/`ForbiddenException`. Referenced by every service Domain (+ global `<Using>`).
  - `TelegramLike.Shared.Application`: `ValidateRequestBeforeHandling` (MediatR pipeline validation), `IntegrationEventMap` (delegate: change event → **the integration events** it becomes; empty keeps it internal — one per publishing service, passed to `AddOutgoingEvents`), `FanoutParts` (splits an embedded audience across several wire messages, [TL-124] — an event that embeds its recipients otherwise grows without bound with the chat; `MaxPerEvent = 500`).
  - `TelegramLike.Shared.Infrastructure`: the **outgoing-events queue** (transactional outbox: `OutgoingEventsStore`/`OutgoingEventsWriter`/`OutgoingEventsSender`, Mongo collection `outgoing_events`, config `OutgoingEvents:*`, wired by `AddOutgoingEvents`). **Draining is not paced by the poll interval** ([TL-125]): a batch is claimed in 3 round-trips (candidates → lease-checked `UpdateMany` with a claim token → read back what this replica won, still exactly-once across replicas), published `PublishConcurrency`-at-a-time (default 4), and the sender loops again immediately while there is work — `PollIntervalSeconds` only governs how often an *empty* queue is re-checked. Concurrency gives up ordering *within* a batch, which nothing relies on (LWW by `OccurredAt`, per-recipient dedup, id-only relays) and which >1 replica already gave up; set `OutgoingEvents:PublishConcurrency=1` for strict order. A published row is **marked** sent, not deleted, so the collection doubles as publish history — bounded by a TTL index on `SentAt` (`OutgoingEvents:SentRetentionDays`, default 7); pending and dead-lettered rows keep `SentAt: null` and are never swept. + `AddMongoDb`/`AddRedis`/`AddRabbitMqBus` setup helpers. Per-service DI lives in each `InfrastructureSetup.cs`.
    - **Mongo indexes are declared, not hand-rolled** ([TL-119]): implement `IMongoIndexes` (collection name + idempotent `EnsureAsync`) and register it with `AddMongoIndexes<T>()`; the shared `MongoIndexInitializer`, registered by `AddMongoDb` itself, applies them all at startup and logs each collection. A service that declares none logs a **warning** — that's the point: Presence was the service that had never written one, and [TL-123] answered that warning. Index tests assert the *plan* (`explain` → no `COLLSCAN`), not just that the index exists — a missing index still returns correct results on a small database. Integration tests call the same `EnsureIndexesAsync` static the declaration wraps.
  - `TelegramLike.Shared.Api`: JWT auth (`AddServiceJwtAuth`) + `ApiControllerBase`. `DomainExceptionFilter` stays per-service on purpose — each maps a different wire contract.
- **Shared infra:** MongoDB (per-service DB, replica set `rs0`), Redis, RabbitMQ (vhost `telegramlike`), Jaeger (OTLP 4317, UI 16686).

## Gateway routing
BFF clients keep their service-relative paths (e.g. `/messages/{id}`); a `ServicePrefixHandler` (inner to the resilience handler) prepends the service prefix (`/messaging`), and the gateway strips it. So the wire path for a service whose route prefix matches its own (chats→`/chats`) is doubled then stripped once: client `/chats/my` → `/chats/chats/my` at the gateway → `/chats/my` at the service.

## Per-area guides (auto-loaded when you work in that directory)
- Web BFF → [src/TelegramLike.Web/CLAUDE.md](src/TelegramLike.Web/CLAUDE.md)
- Client SDK → [src/client/CLAUDE.md](src/client/CLAUDE.md)
- MAUI app → [src/app/CLAUDE.md](src/app/CLAUDE.md)
- Gateway (YARP) → [src/gateway/CLAUDE.md](src/gateway/CLAUDE.md)
- Identity (IdP) → [src/services/identity/CLAUDE.md](src/services/identity/CLAUDE.md)
- Notifications → [src/services/notifications/CLAUDE.md](src/services/notifications/CLAUDE.md)
- Presence → [src/services/presence/CLAUDE.md](src/services/presence/CLAUDE.md)
- Chats → [src/services/chats/CLAUDE.md](src/services/chats/CLAUDE.md)
- Messaging → [src/services/messaging/CLAUDE.md](src/services/messaging/CLAUDE.md)
- Realtime (SignalR hub) → [src/services/realtime/CLAUDE.md](src/services/realtime/CLAUDE.md)

## Auth — Identity is the IdP
- Identity signs short-lived HMAC-SHA256 JWTs: `iss=telegramlike-identity`, `aud=telegramlike-services`, `sub`=userId. Every service validates with the same shared secret and `MapInboundClaims=false`.
- Web holds a cookie session and exchanges the cookie's opaque session token for an access JWT at Identity (`ServiceTokenProvider`, scoped), forwarding `Bearer` on downstream calls. **Web signs nothing.**
- ⚠️ **`ServiceAuth:JwtSecret` is a committed DEV DEFAULT** (same value in every `appsettings.json` and `docker-compose.yml`). Since the scheme is symmetric HMAC, that value **is** the validation key — anyone with the repo/images can forge a token for any `sub` and impersonate any user across every service. Fine for local dev; for any real deployment it must be replaced with a freshly generated secret injected only via env/secret store (never committed) and rotated. Tracked as a known accepted risk for this practice repo.

## Cross-service rule
Never read another service's database. Embed needed data in integration events, or build a local materialized read-model from those events (e.g. Presence's `chat_memberships`). Where a service can't derive a cross-context value itself, the Web BFF enriches the command with it (isBroadcast, isModerator) — but **a service's own read-model wins whenever it has one**: recipients ([TL-118]) and isPremium ([TL-102]) were pulled back server-side precisely because BFF-side enrichment meant every UI host carried its own copy of the derivation.

## Eventing & consistency rules (deliberate exceptions — don't "fix" them)
- **Transactional outbox** is required for state-carrying events — chats and messaging use it. Presence (online/offline/typing) and Notifications (`UnreadCountChanged`) **publish directly by design**: signal-only/ephemeral events where a crash-drop is benign; consumers stay idempotent either way. Identity publishes nothing.
- **Optimistic concurrency** exists only in messaging (`Message.Version` + retry) where concurrent sub-entity edits (reactions/retract) are real; other aggregates are single-writer last-write-wins on purpose.
- **Fan-out events carry a slice, not the room** ([TL-124]). `MessageSent`, `MemberJoined`, `MemberKicked` and `ChatMembershipsSnapshot` embed their audience — that embed is what keeps consumers from cross-reading — so their size followed the size of the chat. They are now split into parts of ≤500 via `FanoutParts`: each part is a complete event with its own `EventId` and a disjoint slice. **A consumer acting once per *recipient* needs no awareness of parts; one acting once per *event* (a chat-wide push) must gate on `PartIndex == 0`.**
- **Relay consumers** (realtime hub, Web pubsub) have no dedup on purpose — pushes are id-only signals and the UI refetches, so a redelivered event self-heals.

## Conventions
- **Commit / iteration prefix: `[TL-N]`** — single running counter (history: Day N → Step N → [TL-N]). Housekeeping (memory/docs sync) → plain `docs:` with no number.
- End commit messages with: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- New service/context = the 6-phase recipe (scaffold+Domain → Application → Infrastructure → Api → Web wiring → cleanup → compose). Details in the `microservices_migration` memory.

## Commands
- **First, always: `./build/pack-shared.ps1`** — publishes the shared layer to the local feed. Nothing that consumes it (solution, tests, Docker images, MAUI) can restore until this has run once, and it must run again after any shared-layer change.
- Build: `dotnet build TelegramLike.sln` (shared layer: `dotnet build TelegramLike.Shared.slnx`)
- Test: `dotnet test` — Infrastructure tests use Testcontainers, so Docker must be running.
- Run the whole stack: `docker compose up -d --build` → http://localhost:18080. **Docker host ports = local-dev ports + 10000** (services 18081–18086, gateway 18090) so the compose stack never clashes with `dotnet run` or other apps; inside containers everything stays on 8080.
- Traces: http://localhost:16686 · RabbitMQ UI: http://localhost:15672 · Grafana: http://localhost:3000 (anon view; admin/admin) · Prometheus: http://localhost:9090 · Alertmanager: http://localhost:9093
- Alert rules live in `monitoring/rules.yml` (TargetDown / HighHttp5xxRate / HighRequestLatencyP95 / OutboxStalled / OutboxDeadLettering); Alertmanager has no external notifier wired locally.
- **Two provisioned dashboards** (`monitoring/grafana/dashboards/`): *Services Overview* (RED — HTTP rate/latency/errors, infra-level) and *Product & Outbox* (business counters + outbox health, `$service`-templated). Adding a custom metric = create a `Meter` (see `MessagingMetrics`/`ChatsMetrics`/`OutboxMetrics`) **and** name it in that service's `WithMetrics(m => m.AddMeter(...))` — an unlisted meter is silently dropped.
- RabbitMQ exposes Prometheus metrics on :15692 (`rabbitmq_prometheus`, on by default). Prometheus scrapes it twice: `/metrics` for cluster aggregates and `/metrics/detailed?family=queue_coarse_metrics` for per-queue depth (`rabbitmq_detailed_queue_messages_ready{queue=...}`).
- Traffic simulation (N SDK bots chatting through the gateway, for watching Grafana/RabbitMQ/Jaeger live): `dotnet run --project tools/TelegramLike.Simulator` — intensity/duration configurable, see `tools/TelegramLike.Simulator/README.md`. Not in the .sln (local tool, like the MAUI app).
- **Docker gotcha:** `docker compose --build` may reuse a cached .NET *publish* layer and silently ship stale service code (fresh image timestamp, old bits). After changing service source, verify or use `docker compose build --no-cache <svc>`.
- **Docker gotcha #2:** service images restore the shared layer from `artifacts/packages`, which they `COPY` from the build context. A stale (or empty) local feed ships stale shared code into the image the same silent way — run `./build/pack-shared.ps1` before `docker compose build`.

## Memory
Project knowledge and decision history live in `.claude/memory/` (index: `MEMORY.md`, auto-loaded each session). Keep it current after notable changes — it complements these directory-scoped `CLAUDE.md` files.
