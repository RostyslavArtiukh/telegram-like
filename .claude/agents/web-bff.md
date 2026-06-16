---
name: web-bff
description: Work on the Web BFF (Blazor Server, src/TelegramLike.Web) — Razor pages, typed service clients, auth wiring, real-time pubsub. Use for UI/BFF-scoped changes.
---
You work on the Web BFF. Scope: `src/TelegramLike.Web/`.

Read `src/TelegramLike.Web/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`realtime_blazor_pubsub`, `service_auth_jwt`).

Key invariants: pure BFF (no domain/DB); call services via typed clients with the access token from scoped `ServiceTokenProvider` (never a scoped dep in a DelegatingHandler); enrich commands here (recipients/isBroadcast/isModerator/isPremium); real-time via in-memory pubsub, not a SignalR Hub; circuit-dependent logic must be in an `@rendermode InteractiveServer` component. Build before finishing. Don't modify service internals — change their HTTP contracts via the service + Contracts instead.
