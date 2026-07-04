# Gateway (YARP reverse proxy, port 8090)

Single entry point for all BFF→service traffic. One project `TelegramLike.Gateway` (ASP.NET Core + `Yarp.ReverseProxy`). **No domain, no DB, no auth logic** — pure routing.

## What it does
- Routes `/<service>/{**catch-all}` → that service's cluster and **strips the prefix** (`PathRemovePrefix`). Prefixes: `identity`, `notifications`, `presence`, `chats`, `messaging`.
- Prefix routing is required because chats and messaging **both** serve `/chats/*` (messaging owns `/chats/{chatId}/messages`) — you can't route those two on the natural path.
- Forwards `Authorization` untouched; each service still validates the Identity-issued JWT. The gateway is not a trust boundary.
- Emits OTel traces (`telegramlike.gateway`) so a request shows Web → gateway → service in Jaeger.
- `/health` + `/health/ready` (liveness only — no backing store).

## Config, not code
Routes + clusters live entirely in `appsettings.json` under `ReverseProxy`. Destinations default to `http://localhost:808x` (local dev against exposed service ports); compose overrides each via `ReverseProxy__Clusters__<name>__Destinations__d1__Address=http://<name>:8080`. Adding/retargeting a service = a config change here + one line in the BFF.

## Pairs with the BFF
The BFF holds one `Gateway:BaseUrl` and a per-client `ServicePrefixHandler` that prepends the prefix the gateway strips. See `src/TelegramLike.Web/CLAUDE.md` and the `bff_resilience` memory. Client paths stay service-relative; the prefix is added by the handler, so for chats/notifications/presence the wire path is doubled (`/chats/chats/my`) then stripped once.
