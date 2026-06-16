# Messaging service (port 8084, DB `telegramlike_messaging`)

Messages, reactions, read receipts. 4 projects, namespace `TelegramLike.Messaging.*`.

## Domain
- `Message` aggregate = one document; `Reaction` embedded. Attachments + reactions are embedded arrays (atomic `$push`/`$pull`). `HideMessage` → `hidden_messages` read-model. Read receipts: Direct/Group → `message_read_receipts`; Broadcast → `$inc broadcastReadCount` on the message.
- Own autonomous outbox. Publishes `MessageSent` (carries **embedded recipients**), `MessageRetracted`, `ReactionAdded/Removed`. Query `GetChatMessages` = keyset paging by `SentAt` DESC.

## BFF-enriched params (no cross-service query)
Commands take what Messaging can't know itself — the Web BFF supplies it:
- `SendMessage(recipients, isBroadcast)` · `AddReaction(actorIsPremium)` · `RetractMessage(actorIsModerator)` · `MarkAsRead(isBroadcast)`.
- **Known regression:** membership is NOT validated here (fail-open) — anyone bypassing the BFF could post/read in any chat. Restoring strict checks needs a local Chats-membership read-model (Presence-style).

## Endpoints
`POST /messages/`, `GET /messages/{id}`, reactions add/remove, `{id}/retract`, `{id}/read`, `{id}/hide`, `GET /chats/{id}/messages`.
