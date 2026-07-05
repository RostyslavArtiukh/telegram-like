# Messaging service (port 8084, DB `telegramlike_messaging`)

Messages, reactions, read receipts. 4 projects, namespace `TelegramLike.Messaging.*`.

## Domain
- `Message` aggregate = one document; `Reaction` embedded. Attachments + reactions are embedded arrays (atomic `$push`/`$pull`). `HideMessage` → `hidden_messages` read-model. Read receipts: Direct/Group → `message_read_receipts`; Broadcast → `$inc broadcastReadCount` on the message.
- Own autonomous outbox. Publishes `MessageSent` (carries **embedded recipients**), `MessageRetracted`, `ReactionAdded/Removed`. Query `GetChatMessages` = keyset paging by `SentAt` DESC.

## BFF-enriched params (no cross-service query)
Commands take what Messaging can't know itself — the Web BFF / SDK supplies it:
- `SendMessage(recipients, isBroadcast)` · `AddReaction(actorIsPremium)` · `RetractMessage(actorIsModerator)` · `MarkAsRead(isBroadcast)`.
- **SendMessage is now hybrid fail-closed** ([TL-70]): a local `chat_memberships` read-model (event-sourced from `MemberJoined/Kicked/Left`, Presence-style) drives it. If the chat is materialized and the author isn't a member → `UnauthorizedAccessException` (403). Recipients are **derived server-side** from the read-model for known chats (caller's `recipients` ignored → no spoofing); the caller's list is used only while a chat is still unknown (legacy / MemberJoined in flight), where it stays fail-open to avoid rejecting a just-created chat's first send.
- **Still fail-open (deferred):** `AddReaction` / `RetractMessage` / `MarkAsRead` don't check membership; `isBroadcast` / `isModerator` / `isPremium` are still caller-supplied (they need role/type data the read-model doesn't hold yet).

## Endpoints
`POST /messages/`, `GET /messages/{id}`, reactions add/remove, `{id}/retract`, `{id}/read`, `{id}/hide`, `GET /chats/{id}/messages`.

Controllers (`Controllers/`): `MessagesController` (lifecycle + queries) + `MessageReactionsController` + `MessageReadReceiptsController`, on `ApiControllerBase`; `DomainExceptionFilter` (`InvalidOperationException`/`ArgumentException`→400, `UnauthorizedAccessException`→403, `ProblemDetails`). `JsonStringEnumConverter` kept — **load-bearing** for `Emoji`/`AttachmentType` on the wire (the BFF sends/reads names). BFF-enriched params live in the `Contracts/` request records. See the `api_controllers` memory.
