# Chats service (port 8083, DB `telegramlike_chats`)

Chats, members, roles. 4 projects, namespace `TelegramLike.Chats.*`.

## Domain
- `Chat` (abstract) → `DirectChat` / `GroupChat` / `BroadcastChannel`. `Member` is an entity persisted in a **separate `chat_members` collection** (not embedded — scales for large groups).
- `ChatRepository.Add/Update` run a **multi-document Mongo transaction** (`IClientSessionHandle` + `WithTransactionAsync`) and drain domain events into the outbox in the same txn. Member upserts via `BulkWrite` + `ReplaceOneModel{IsUpsert=true}` (Left/Kicked/Banned keep the row).
- Broadcast: join → Viewer; roles via Promote/Demote (not arbitrary ChangeRole). Direct chats reject rename/kick/leave.

## Events / outbox
Own **autonomous outbox bundle** (full copy, not shared). Publishes `MemberJoined/Kicked/Left` + chat-lifecycle events. Actor comes from the JWT `sub`; there is no `IUserRepository` here (Identity is separate) — callers are trusted via JWT.

## Endpoints (`/chats`, authed)
`my`, `{id}`, `{id}/members`, create `direct|group|broadcast`, `{id}/join`, `{id}/leave`, members `{u}/kick` · `{u}/role`, `transfer-ownership`, `PATCH {id}` (rename).
