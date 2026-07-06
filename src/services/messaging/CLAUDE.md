# Messaging service (port 8084, DB `telegramlike_messaging`)

Messages, reactions, read receipts. 4 projects, namespace `TelegramLike.Messaging.*`.

## Domain
- `Message` aggregate = one document; `Reaction` embedded. Reaction/retract writes go through the aggregate and use **optimistic concurrency** ([TL-74b]): a `Version` field guards the `ReplaceOne` and callers retry via `ConcurrencyRetry` — so concurrent reactions don't lost-update. `HideMessage` → `hidden_messages` read-model. Read receipts: unique `(MessageId, MemberId)` index makes them idempotent; Broadcast additionally does an atomic `$inc broadcastReadCount` on the message, once per new receipt.
- Own autonomous outbox. Publishes `MessageSent` (carries **embedded recipients**), `MessageRetracted`, `ReactionAdded/Removed`. Query `GetChatMessages` = keyset paging by `SentAt` DESC.

## BFF-enriched params (no cross-service query)
Commands take what Messaging can't know itself — the Web BFF / SDK supplies it:
- `SendMessage(recipients, isBroadcast)` · `AddReaction(actorIsPremium)` · `RetractMessage(actorIsModerator)` · `MarkAsRead(isBroadcast)`.
- **SendMessage is now hybrid fail-closed** ([TL-70]): a local `chat_memberships` read-model (event-sourced from `MemberJoined/Kicked/Left`, Presence-style) drives it. If the chat is materialized and the author isn't a member → `UnauthorizedAccessException` (403). Recipients are **derived server-side** from the read-model for known chats (caller's `recipients` ignored → no spoofing); the caller's list is used only while a chat is still unknown (legacy / MemberJoined in flight), where it stays fail-open to avoid rejecting a just-created chat's first send.
- **Retract moderator authority is server-side** ([TL-74b]): the read-model materializes member `Role` (from `MemberJoined` + `MemberRoleChanged`), and `RetractMessage` derives Owner/Admin via `IsModeratorAsync` — the caller's `actorIsModerator` flag is ignored (kept on the wire for compat). Reads (`GetChatMessages` / `GetMessageById`) enforce membership fail-closed for materialized chats.
- **Still fail-open (deferred):** `AddReaction` / `MarkAsRead` don't reject non-members (log-only); `isBroadcast` / `isPremium` are still caller-supplied (broadcast type + premium status aren't in the read-model yet).

## Endpoints
`POST /messages/`, `GET /messages/{id}`, reactions add/remove, `{id}/retract`, `{id}/read`, `{id}/hide`, `GET /chats/{id}/messages`.

Controllers (`Controllers/`): `MessagesController` (lifecycle + queries) + `MessageReactionsController` + `MessageReadReceiptsController`, on `ApiControllerBase`; `DomainExceptionFilter` (`InvalidOperationException`/`ArgumentException`→400, `UnauthorizedAccessException`→403, `ProblemDetails`). `JsonStringEnumConverter` kept — **load-bearing** for `Emoji`/`AttachmentType` on the wire (the BFF sends/reads names). BFF-enriched params live in the `Contracts/` request records. See the `api_controllers` memory.
