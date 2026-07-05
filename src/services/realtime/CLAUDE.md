# Realtime service (port 8086, no DB)

SignalR push channel for **external clients** (MAUI/desktop via the `TelegramLike.Client` SDK). One project, `TelegramLike.Realtime.Api` — no Domain/Application/Infrastructure split because there is no domain: it only relays RabbitMQ integration events into hub groups. The Web BFF does NOT use it (Blazor circuits keep the in-proc pubsub — see `realtime_blazor_pubsub` memory).

## How it works
- Hub at `/hub` (`/realtime/hub` through the gateway — YARP proxies the WebSocket). `[Authorize]` with the standard Identity JWT; WebSocket clients send the token as `?access_token=` (handled in `JwtBearerEvents.OnMessageReceived`, hub path only).
- **Groups:** on connect every connection joins `user:{sub}` (raw `sub` claim — `MapInboundClaims=false`, so don't use `ClaimTypes.NameIdentifier`). Clients call `JoinChat`/`LeaveChat` for `chat:{chatId}` while a chat is open.
- **Consumers** (`Consumers/`) relay events → groups. Two-event split avoids double delivery: `MessageSent` → chat group (open-chat view), `ChatActivity` → user groups of recipients + author (chat list/badges). Reactions/retract/typing → chat group; presence on/offline → `Clients.All`; `UnreadCountChanged` → user groups, signal-only.
- **Per-instance temporary queues** (same as Web [TL-63]): each replica must see every event because it only pushes to ITS connections. Don't switch to shared durable queues.
- Push payload shapes + event names live in `Contracts/Realtime/RealtimeEvents.cs` — shared with the SDK's `TelegramLikeRealtimeClient`, so they can't drift. Changing them is a breaking change for deployed apps.

## Known trade-offs (deliberate)
- `JoinChat` does not validate chat membership (same trust model as Presence.StartTyping) — tracked together with the messaging enrichment fail-open.
- Reconnect loses group membership; the SDK client re-joins its open chats on `Reconnected`.
- Health: `/health/ready` = RabbitMQ bus only (MassTransit's auto check); no DB to probe.
