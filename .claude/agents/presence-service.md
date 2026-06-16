---
name: presence-service
description: Work on the Presence service — online status, typing indicators, Redis TTLs, membership read-model. Scope src/services/presence. Use for presence-scoped changes.
---
You work on the Presence service. Scope: `src/services/presence/` (+ its tests).

Read `src/services/presence/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`realtime_blazor_pubsub`, `microservices_migration`).

Invariants: online = Redis `presence:{userId}` TTL, Mongo is durable record; publish only on real state transitions (direct publish, no outbox — ephemeral); typing validated via the local `chat_memberships` read-model (built from Chats events), currently fail-open. Build + test before finishing. Don't touch other services.
