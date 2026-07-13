# Messaging service (port 8084, DB `telegramlike_messaging`)

Messages, reactions, read receipts. 4 projects, namespace `TelegramLike.Messaging.*`.

## Domain
- `Message` aggregate = one document; `Reaction` embedded. Reaction/retract writes go through the aggregate and use **optimistic concurrency** ([TL-74b]): a `Version` field guards the `ReplaceOne` and callers retry via `ConcurrencyRetry` — so concurrent reactions don't lost-update. `HideMessage` → `hidden_messages` read-model. Read receipts: unique `(MessageId, MemberId)` index makes them idempotent; Broadcast additionally does an atomic `$inc broadcastReadCount` on the message, once per new receipt.
- Publishes through the shared outgoing-events queue (`TelegramLike.Infrastructure.ServiceDefaults`, collection `outgoing_events`). Publishes `MessageSent` (carries **embedded recipients**), `MessageRetracted`, `ReactionAdded/Removed`. Query `GetChatMessages` = keyset paging by `SentAt` DESC.

## BFF-enriched params (no cross-service query)
Commands take what Messaging can't know itself — the Web BFF / SDK supplies it:
- `SendMessage(recipients, isBroadcast)` · `AddReaction(userIsPremium)` · `RetractMessage(retractedByModerator)` · `MarkAsRead(isBroadcast)`.
- **SendMessage is now hybrid fail-closed** ([TL-70]): a local `chat_memberships` read-model (event-sourced from `MemberJoined/Kicked/Left`, Presence-style) drives it. If the chat is materialized and the author isn't a member → `UnauthorizedAccessException` (403). Recipients are **derived server-side** from the read-model for known chats (caller's `recipients` ignored → no spoofing); the caller's list is used only while a chat is still unknown (legacy / MemberJoined in flight), where it stays fail-open to avoid rejecting a just-created chat's first send.
- **Retract moderator authority is server-side** ([TL-74b]): the read-model materializes member `Role` (from `MemberJoined` + `MemberRoleChanged`), and `RetractMessage` derives Owner/Admin via `IsModeratorAsync` — the caller's `retractedByModerator` flag is ignored (kept on the wire for compat). Reads (`GetChatMessages` / `GetMessageById`) enforce membership fail-closed for materialized chats.
- **Membership now fail-closed** ([TL-101]): `AddReaction` / `RemoveReaction` / `MarkAsRead` and `RetractMessage`'s membership check reject non-members with `ForbiddenException` (403) — safe now that the `chat_memberships` read-model is backfilled for pre-existing chats via the admin-triggered `ChatMembershipsSnapshotIntegrationEvent`.
- **Still caller-supplied (deferred to server-side enrichment):** `isBroadcast` / `isPremium` (broadcast type + premium status aren't in the read-model yet).

## Endpoints
`POST /messages/`, `GET /messages/{id}`, reactions add/remove, `{id}/retract`, `{id}/read`, `{id}/hide`, `GET /chats/{id}/messages`.

Controllers (`Controllers/`): `MessagesController` (lifecycle + queries) + `MessageReactionsController` + `MessageReadReceiptsController`, on `ApiControllerBase`; `DomainExceptionFilter` (`ValidationException`/`DomainException`→400, `ForbiddenException`→403, `ProblemDetails` + `traceId`); handlers/domain throw those semantic types (in `Messaging.Domain`), **not** raw BCL exceptions — a framework `InvalidOperationException`/`ArgumentException` now → 500. `JsonStringEnumConverter` kept — **load-bearing** for `Emoji`/`AttachmentType` on the wire (the BFF sends/reads names). BFF-enriched params live in the `Contracts/` request records. See the `api_controllers` memory.
