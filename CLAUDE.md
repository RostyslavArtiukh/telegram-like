# TelegramLike — project guide

Telegram-like messenger. Migrated from a modular monolith to **microservices + a Blazor Server BFF**. DDD, CQRS (MediatR inside services), event-driven (RabbitMQ/MassTransit + transactional outbox).

## Architecture
- **Web BFF** — `src/TelegramLike.Web`, port 8080. Blazor Server, pure BFF: no domain, no DB. Talks to services over HTTP; hosts a MassTransit bus only for real-time pubsub consumers. See its own `CLAUDE.md`.
- **5 services** — `src/services/<name>/`, each = Domain/Application/Infrastructure/Api, own Mongo DB, own port:
  - identity **8085** · notifications **8081** · presence **8082** · chats **8083** · messaging **8084**
- **Shared infra:** MongoDB (per-service DB, replica set `rs0`), Redis, RabbitMQ (vhost `telegramlike`), Jaeger (OTLP 4317, UI 16686).

## Auth — Identity is the IdP
- Identity signs short-lived HMAC-SHA256 JWTs: `iss=telegramlike-identity`, `aud=telegramlike-services`, `sub`=userId. Every service validates with the same shared secret and `MapInboundClaims=false`.
- Web holds a cookie session and exchanges the cookie's opaque session token for an access JWT at Identity (`ServiceTokenProvider`, scoped), forwarding `Bearer` on downstream calls. **Web signs nothing.**

## Cross-service rule
Never read another service's database. Embed needed data in integration events, or build a local materialized read-model from those events (e.g. Presence's `chat_memberships`). The Web BFF enriches commands with cross-context data (recipients, isBroadcast, isModerator, isPremium).

## Conventions
- **Commit / iteration prefix: `[TL-N]`** — single running counter (history: Day N → Step N → [TL-N]). Housekeeping (memory/docs sync) → plain `docs:` with no number.
- End commit messages with: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- New service/context = the 6-phase recipe (scaffold+Domain → Application → Infrastructure → Api → Web wiring → cleanup → compose). Details in the `microservices_migration` memory.

## Commands
- Build: `dotnet build TelegramLike.sln`
- Test: `dotnet test` — Infrastructure tests use Testcontainers, so Docker must be running.
- Run the whole stack: `docker compose up -d --build` → http://localhost:8080
- Traces: http://localhost:16686 · RabbitMQ UI: http://localhost:15672

## Memory
Project knowledge and decision history live in `.claude/memory/` (index: `MEMORY.md`, auto-loaded each session). Keep it current after notable changes — it complements these directory-scoped `CLAUDE.md` files.
