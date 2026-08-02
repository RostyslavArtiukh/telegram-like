# Realtime service (port 8086, no DB)

SignalR push channel for **external clients** (MAUI/desktop via the `TelegramLike.Client` SDK). One project, `TelegramLike.Realtime.Api` — no Domain/Application/Infrastructure split because there is no domain: it only relays RabbitMQ integration events into hub groups. The Web BFF does NOT use it (Blazor circuits keep the in-proc pubsub — see `realtime_blazor_pubsub` memory).

## How it works
- Hub at `/hub` (`/realtime/hub` through the gateway — YARP proxies the WebSocket). `[Authorize]` with the standard Identity JWT; WebSocket clients send the token as `?access_token=` (handled in `JwtBearerEvents.OnMessageReceived`, hub path only).
- **Groups:** on connect every connection joins `user:{sub}` (raw `sub` claim — `MapInboundClaims=false`, so don't use `ClaimTypes.NameIdentifier`). Clients call `JoinChat`/`LeaveChat` for `chat:{chatId}` while a chat is open.
- **Consumers** (`Consumers/`) relay events → groups. Two-event split avoids double delivery: `MessageSent` → chat group (open-chat view), `ChatActivity` → user groups of recipients + author (chat list/badges). A large chat's `MessageSentIntegrationEvent` arrives as **several parts** ([TL-124]): the chat-group push is per message so it fires only on `PartIndex == 0` (and the author rides along there), while `ChatActivity` fires per part over that part's slice. Reactions/retract/typing → chat group; presence on/offline → `Clients.All`; `UnreadCountChanged` → user groups, signal-only.
- **Per-instance temporary queues** (same as Web [TL-63]): each replica must see every event because it only pushes to ITS connections. Don't switch to shared durable queues.
- Push payload shapes + event names live in `Contracts/Realtime/RealtimeEvents.cs` — shared with the SDK's `TelegramLikeRealtimeClient`, so they can't drift. Changing them is a breaking change for deployed apps.

## Known trade-offs (deliberate)
- `JoinChat` authorizes through `Membership/ChatMembershipCheck` ([TL-127]): answer from what this replica already knows, otherwise **ask Chats** (`GET /chats/{id}`, which is member-only and hides a non-member's chat as 404) forwarding **the connecting user's own token** — so this service holds no credentials and can grant nothing the user couldn't already read. Straight to Chats via `Chats:BaseUrl`, not through the gateway: the gateway already waits on this service in compose, so that would be a cycle.
  - **Events refresh answers, they never create them.** Caching a pair nobody here asked about would rebuild what this was before — a full in-memory copy of every membership in the system, on every replica, blind after a restart until a human re-ran the admin backfill. There is deliberately **no snapshot-backfill consumer** here any more.
  - `ChatDeleted` → `Revoke` is load-bearing, not defensive: Chats' own member lookup ignores `DeletedAt`, so asking it about a soft-deleted chat still answers "member".
  - Fail-open survives only for "**couldn't ask**" (Chats unreachable/timed out) — transient and logged, not the standing state of every unobserved chat.
- **A ban/kick does not evict a live connection from its `chat:{id}` group** — nothing maps a userId back to its connection ids. Until the banned user reconnects they keep receiving that chat's id-only pushes; content stays protected by Messaging's fail-closed reads. [TL-76]
- Reconnect loses group membership; the SDK client re-joins its open chats on `Reconnected`.
- Health: `/health/ready` = RabbitMQ bus only (MassTransit's auto check); no DB to probe.
