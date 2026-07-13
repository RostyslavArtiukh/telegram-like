# TelegramLike — project guide

Telegram-like messenger. Migrated from a modular monolith to **microservices + a Blazor Server BFF**. DDD, CQRS (MediatR inside services), event-driven (RabbitMQ/MassTransit + transactional outbox).

## Architecture
- **Web BFF** — `src/TelegramLike.Web`, port 8080. Blazor Server, pure BFF: no domain, no DB. Talks to services **through the gateway** over HTTP (one `Gateway:BaseUrl`, not five service URLs); hosts a MassTransit bus only for real-time pubsub consumers. See its own `CLAUDE.md`.
- **Gateway** — `src/gateway/TelegramLike.Gateway`, port **8090**. YARP reverse proxy: routes `/<service>/**` to each service and strips the prefix (routes generated in `Program.cs` from one `backends` list; only destination addresses stay config/env-overridable). Does no auth — forwards `Authorization` untouched; each service validates the JWT. Needed because chats and messaging both serve `/chats/*`. See `bff_resilience` + the routing note below.
- **Client SDK** — `src/client/TelegramLike.Client`. NuGet-packable typed clients for all 5 services (via the gateway) + auth flow + SignalR realtime client; consumed by the Web BFF and the MAUI app. See `src/client/CLAUDE.md`.
- **MAUI app** — `src/app/TelegramLike.App`. Blazor Hybrid desktop (Windows now, Android next) built purely on the SDK. **Not in `TelegramLike.sln`** (CI is ubuntu) — use `TelegramLike.App.slnx`. See `src/app/CLAUDE.md`.
- **5 services** — `src/services/<name>/`, each = Domain/Application/Infrastructure/Api, own Mongo DB, own port:
  - identity **8085** · notifications **8081** · presence **8082** · chats **8083** · messaging **8084**
- **Realtime** — `src/services/realtime`, port **8086**. Single-project SignalR hub for external clients (SDK/MAUI): relays integration events into per-user/per-chat hub groups. No DB, no domain. The Web BFF does not use it. See `src/services/realtime/CLAUDE.md`.
- **Shared projects** — `src/shared/`, one per layer so a layer only drags its own dependencies:
  - `TelegramLike.Domain.ServiceDefaults` (zero deps): `ObjectWithId` (id + equality), `ObjectWithEvents` (records `IChangeEvent`s via `RecordEvent`/`PendingEvents`/`ClearPendingEvents`), `DomainException`/`ForbiddenException`. Referenced by every service Domain (+ global `<Using>`).
  - `TelegramLike.Application.ServiceDefaults`: `ValidateRequestBeforeHandling` (MediatR pipeline validation), `IIntegrationEventMapper` (change event → integration event, keyed by `ChangeEventType`).
  - `TelegramLike.Infrastructure.ServiceDefaults`: the **outgoing-events queue** (transactional outbox: `OutgoingEventsStore`/`OutgoingEventsWriter`/`OutgoingEventsSender`, Mongo collection `outgoing_events`, config `OutgoingEvents:*`, wired by `AddOutgoingEvents`) + `AddMongoDb`/`AddRedis`/`AddRabbitMqBus` setup helpers. Per-service DI lives in each `InfrastructureSetup.cs`.
  - `TelegramLike.Api.ServiceDefaults`: JWT auth (`AddServiceJwtAuth`) + `ApiControllerBase`. `DomainExceptionFilter` stays per-service on purpose — each maps a different wire contract.
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
- ⚠️ **`ServiceAuth:JwtSecret` is a committed DEV DEFAULT** (same value in every `appsettings.json`, `docker-compose.yml`, and `k8s/01-secret.yaml`). Since the scheme is symmetric HMAC, that value **is** the validation key — anyone with the repo/images can forge a token for any `sub` and impersonate any user across every service. Fine for local dev; for any real deployment it must be replaced with a freshly generated secret injected only via env/secret store (never committed) and rotated. Tracked as a known accepted risk for this practice repo.

## Cross-service rule
Never read another service's database. Embed needed data in integration events, or build a local materialized read-model from those events (e.g. Presence's `chat_memberships`). The Web BFF enriches commands with cross-context data (recipients, isBroadcast, isModerator, isPremium).

## Eventing & consistency rules (deliberate exceptions — don't "fix" them)
- **Transactional outbox** is required for state-carrying events — chats and messaging use it. Presence (online/offline/typing) and Notifications (`UnreadCountChanged`) **publish directly by design**: signal-only/ephemeral events where a crash-drop is benign; consumers stay idempotent either way. Identity publishes nothing.
- **Optimistic concurrency** exists only in messaging (`Message.Version` + retry) where concurrent sub-entity edits (reactions/retract) are real; other aggregates are single-writer last-write-wins on purpose.
- **Relay consumers** (realtime hub, Web pubsub) have no dedup on purpose — pushes are id-only signals and the UI refetches, so a redelivered event self-heals.

## Conventions
- **Commit / iteration prefix: `[TL-N]`** — single running counter (history: Day N → Step N → [TL-N]). Housekeeping (memory/docs sync) → plain `docs:` with no number.
- End commit messages with: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- New service/context = the 6-phase recipe (scaffold+Domain → Application → Infrastructure → Api → Web wiring → cleanup → compose). Details in the `microservices_migration` memory.

## Commands
- Build: `dotnet build TelegramLike.sln`
- Test: `dotnet test` — Infrastructure tests use Testcontainers, so Docker must be running.
- Run the whole stack: `docker compose up -d --build` → http://localhost:18080. **Docker host ports = local-dev ports + 10000** (services 18081–18086, gateway 18090) so the compose stack never clashes with `dotnet run` or other apps; inside containers everything stays on 8080.
- **Kubernetes** (mirror of compose): `docker compose build` then `kubectl apply -k .` (kustomization at repo root, namespace `telegramlike`) → web at http://localhost:30080 (NodePort); everything else via `kubectl port-forward`. See `k8s/README.md`.
- Traces: http://localhost:16686 · RabbitMQ UI: http://localhost:15672 · Grafana: http://localhost:3000 (anon view; admin/admin) · Prometheus: http://localhost:9090 · Alertmanager: http://localhost:9093
- Alert rules live in `monitoring/rules.yml` (TargetDown / HighHttp5xxRate / HighRequestLatencyP95); Alertmanager has no external notifier wired locally.
- **Docker gotcha:** `docker compose --build` may reuse a cached .NET *publish* layer and silently ship stale service code (fresh image timestamp, old bits). After changing service source, verify or use `docker compose build --no-cache <svc>`.

## Memory
Project knowledge and decision history live in `.claude/memory/` (index: `MEMORY.md`, auto-loaded each session). Keep it current after notable changes — it complements these directory-scoped `CLAUDE.md` files.
