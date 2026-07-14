---
name: api-gateway
description: YARP reverse-proxy gateway in front of the 6 backends (5 services + realtime hub); BFF routes all downstream calls through it
metadata: 
  node_type: memory
  type: project
  originSessionId: 04d4d75d-4c51-4ce4-8177-ebc55969c752
---

**[TL-56] (2026-07-04):** Added `src/gateway/TelegramLike.Gateway` — YARP reverse proxy, port **8090** (host) / 8080 (container). Single entry point for all BFF→service traffic. Pure routing: no domain, no DB, no auth logic.

**Routing:** `/<service>/{**catch-all}` → service cluster, `PathRemovePrefix` strips the prefix. Prefixes: identity/notifications/presence/chats/messaging + realtime (`/realtime/hub`, [TL-65] — WebSocket/SignalR YARP проксує з коробки). **Why prefix-routing (not natural paths):** chats and messaging both serve `/chats/*` (messaging owns `/chats/{chatId}/messages`) — ambiguous otherwise.

**Code, config-overridable (з [TL-91]):** routes+clusters генеруються у `Program.cs` з одного списку `backends`; у config лишаються тільки destination-адреси (default `http://localhost:808x` для local dev; compose перекриває через `ReverseProxy__Clusters__<name>__Destinations__d1__Address`). До [TL-91] усе жило в `appsettings.json`. Emits OTel traces (`telegramlike.gateway`). `/health` + `/health/ready` — обидва статичні "ok" (немає бекенд-стору); liveness `/health` + readiness `/health/ready` ([TL-99]).

**Auth:** gateway forwards `Authorization` untouched; each service still validates the Identity JWT. Gateway is NOT a trust boundary.

**BFF pairing:** five `*Api:BaseUrl` settings collapsed into one `Gateway:BaseUrl`. `ServicePrefixHandler` (`Web/Services/Resilience/`) prepends each client's prefix; registered **inner** to the resilience handler so retries clone the un-prefixed request and never double the prefix. All 38 service-relative client paths untouched. Consequence: for a service whose route prefix == its own path prefix (chats/notifications/presence) the wire path is doubled then stripped once (`/chats/my` → gateway `/chats/chats/my` → service `/chats/my`).

**Package:** `Yarp.ReverseProxy` 2.3.0. Project targets net9.0 (dotnet new defaulted to net10 — had to fix csproj).

**Verified live (full compose stack):** all containers healthy incl. gateway; web serves (302); gateway routes every `/<svc>/health` (200); authed paths → 401 (routed, not 404) incl. doubled-prefix; full round-trip through containerized gateway — register→login→JWT exchange→`GET /chats/chats/my` Bearer → 200 `[]`.

See [[microservices-migration]], [[bff-resilience]].
