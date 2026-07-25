# Chats service (port 8083, DB `telegramlike_chats`)

Chats, members, roles. 4 projects, namespace `TelegramLike.Chats.*`.

## Domain
- `Chat` (abstract) → `DirectChat` / `GroupChat` / `BroadcastChannel`. `Member` is an entity persisted in a **separate `chat_members` collection** (not embedded — scales for large groups).
- `ChatRepository.Add/Update` run a **multi-document Mongo transaction** (`IClientSessionHandle` + `WithTransactionAsync`) and drain pending events into the shared outgoing-events queue in the same txn. Member upserts via `BulkWrite` + `ReplaceOneModel{IsUpsert=true}` (Left/Kicked/Banned keep the row).
- Broadcast: join → Viewer; roles via Promote/Demote (not arbitrary ChangeRole). Direct chats reject rename/kick/leave.

## Events / outgoing-events queue
Publishes through the **shared outgoing-events queue** (transactional outbox in `TelegramLike.Shared.Infrastructure`: `OutgoingEventsStore`/`Writer`/`Sender`, Mongo collection `outgoing_events`) + its own `IIntegrationEventMapper`s. Publishes `MemberJoined/Kicked/Left` + chat-lifecycle events. Actor comes from the JWT `sub`; there is no `IUserRepository` here (Identity is separate) — callers are trusted via JWT.

## Endpoints (`/chats`, authed)
`my`, `{id}`, `{id}/members`, create `direct|group|broadcast`, `{id}/join`, `{id}/leave`, members `{u}/kick` · `{u}/role`, `transfer-ownership`, `PATCH {id}` (rename).

Controllers (`Controllers/`): `ChatsController` (lifecycle + queries) + `ChatMembersController` (membership), both on `ApiControllerBase`; errors via global `DomainExceptionFilter` (`DomainException`→400, `ForbiddenException`→403, `ProblemDetails` + `traceId`); handlers/domain throw those semantic types (in `Chats.Domain`), **not** raw BCL exceptions — a framework `InvalidOperationException`/`ArgumentException` now → 500, not a mislabelled 400. Request records in `Contracts/`. See the `api_controllers` memory.
