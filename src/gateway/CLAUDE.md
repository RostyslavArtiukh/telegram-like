# Gateway (YARP reverse proxy, port 8090)

Single entry point for all BFF→service traffic. One project `TelegramLike.Gateway` (ASP.NET Core + `Yarp.ReverseProxy`). **No domain, no DB, no auth logic** — pure routing.

## What it does
- Routes `/<service>/{**catch-all}` → that service's cluster and **strips the prefix** (`PathRemovePrefix`). Prefixes: `identity`, `notifications`, `presence`, `chats`, `messaging`, `realtime`.
- `realtime` is a WebSocket route (SignalR hub at `/realtime/hub`) — YARP proxies WS upgrades out of the box, nothing special configured.
- Prefix routing is required because chats and messaging **both** serve `/chats/*` (messaging owns `/chats/{chatId}/messages`) — you can't route those two on the natural path.
- Forwards `Authorization` untouched; each service still validates the Identity-issued JWT. The gateway is not a trust boundary.
- Emits OTel traces (`telegramlike.gateway`) so a request shows Web → gateway → service in Jaeger.
- `/health` + `/health/ready` (liveness only — no backing store).

## Routes generated from one list
Routes + clusters are built in code (`GatewayRouting.cs`, `AddGatewayReverseProxy` → `LoadFromMemory`) from a single `Backends` array of `(prefix, defaultAddress)` — every route is the identical shape (match `/<prefix>/**`, strip the prefix, forward to a same-named cluster), so we generate rather than repeat six near-identical JSON blocks. Adding/retargeting a service = one line in that array + one line in the BFF. Only the destination **address** is environment-specific and stays config-overridable via `ReverseProxy:Clusters:<name>:Destinations:d1:Address` — the default is `http://localhost:808x` (local dev), and compose overrides each with `ReverseProxy__Clusters__<name>__Destinations__d1__Address=http://<name>:8080`. (Trade-off: `LoadFromMemory` is static, so no config hot-reload of routes — fine, routes are compile-time constants now.)

## Pairs with the BFF
The BFF holds one `Gateway:BaseUrl` and a per-client `ServicePrefixHandler` that prepends the prefix the gateway strips. See `src/TelegramLike.Web/CLAUDE.md` and the `bff_resilience` memory. Client paths stay service-relative; the prefix is added by the handler, so for chats/notifications/presence the wire path is doubled (`/chats/chats/my`) then stripped once.
