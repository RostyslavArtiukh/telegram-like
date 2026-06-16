# Presence service (port 8082, DB `telegramlike_presence`, + Redis)

Online status + typing indicators. 4 projects, namespace `TelegramLike.Presence.*`.

## Behaviour
- **Online** = Redis key `presence:{userId}` with TTL (`Presence:HeartbeatTtlSeconds`, ~30s); Mongo `user_presence` is the durable record. Heartbeat refreshes the key. Only the offline→online transition publishes `UserCameOnlineIntegrationEvent`; `GoOffline` publishes `UserWentOffline`. Direct publish (no outbox) — presence is ephemeral.
- **Typing** = Redis `typing:{chatId}:{userId}` TTL ~5s; `StartTyping` also publishes `UserTypingIntegrationEvent`.
- **Local membership read-model** (`chat_memberships`, composite id `chatId:userId`) built from `MemberJoined/Kicked/Left` events — lets `StartTyping` check membership without calling Chats. Currently **fail-open** (unknown pair → allow + warn) until a backfill exists.

## Endpoints (`/presence`, authed)
`heartbeat`, `offline`, `{userId}`, `typing/{chatId}/start|stop`, `typing/{chatId}`, `batch` (POST `[ids]` → `{id:isOnline}`).
