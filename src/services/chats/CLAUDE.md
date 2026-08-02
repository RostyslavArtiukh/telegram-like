# Chats service (port 8083, DB `telegramlike_chats`)

Chats, members, roles. 4 projects, namespace `TelegramLike.Chats.*`.

## Domain
- `Chat` (abstract) → `DirectChat` / `GroupChat` / `BroadcastChannel`. `Member` is an entity persisted in a **separate `chat_members` collection** (not embedded — scales for large groups).
- `ChatRepository.Add/Update` run a **multi-document Mongo transaction** (`IClientSessionHandle` + `WithTransactionAsync`) and drain pending events into the shared outgoing-events queue in the same txn. Member upserts via `BulkWrite` + `ReplaceOneModel{IsUpsert=true}` (Left/Kicked/Banned keep the row).
- **One row per (chat, user) — `Member.Rejoin`, not a replacement row.** `Update` upserts by member row id and never deletes, so a rejoin that minted a fresh `Member` grew `chat_members` by a document per leave/rejoin cycle, forever. Ghost rows make `FindAnyMember` (a `FirstOrDefault`) order-dependent, which is how `Ban` could mark a stale `Left` row Banned while the member's live row stayed Active — a silently ineffective ban. `Join` now revives the existing row (resetting role/status/`JoinedAt` and clearing the departure trail); `ChatIndexInitializer` enforces it with a unique `(ChatId, UserId)` index and prunes pre-fix duplicates first (a ban outranks an active row, then newest join wins) so the index can be built on an already-polluted database.
- Broadcast: join → Viewer; roles via Promote/Demote (not arbitrary ChangeRole). Direct chats reject rename/kick/leave.

## Events / outgoing-events queue
Publishes through the **shared outgoing-events queue** (transactional outbox in `TelegramLike.Shared.Infrastructure`: `OutgoingEventsStore`/`Writer`/`Sender`, Mongo collection `outgoing_events`). Publishes `ChatCreated/Deleted` + `MemberJoined/Kicked/Left/Banned/RoleChanged`.

**One switch, not a class per event.** `ChatsIntegrationEvents.Map` (`Application/IntegrationEvents/`) is the whole published surface of this service, handed to `AddOutgoingEvents`. It returns a **list** since [TL-124]: `MemberJoined`/`MemberKicked` embed the audience to notify, so a large chat's event is split into parts of ≤500 via `FanoutParts` (empty list = deliberately internal). The admin membership backfill splits its snapshot the same way. The translation step is load-bearing — `Contracts` has no project references (it ships in the client SDK), so `MemberRole`/`ChatType` must become strings, and `MemberRoleChanged` is narrowed on the way out (`OldRole`/`ChangedBy` stay internal). An event that falls through to the default arm is deliberately kept inside the service.

⚠️ **A change event with no arm is dropped silently** — it never reaches the outbox and no downstream read-model learns of it. `ChatDeleted` and `MemberBanned` sat unpublished for a long time, which is why a ban blocked rejoining but let the banned user keep posting. `ChatsIntegrationEventsTests` pins it: every `IChangeEvent` in the domain is instantiated and run through the real map, and must either produce an integration event or be listed in `DeliberatelyInternal` with a reason. Currently exempt: `ChatRenamedEvent` (no service stores names) and `OwnershipTransferredEvent` (the two `MemberRoleChanged` events already carry the roles). Actor comes from the JWT `sub`; there is no `IUserRepository` here (Identity is separate) — callers are trusted via JWT.

## Endpoints (`/chats`, authed)
`my`, `{id}`, `{id}/members`, create `direct|group|broadcast`, `{id}/join`, `{id}/leave`, members `{u}/kick` · `{u}/ban` · `{u}/role`, `transfer-ownership`, `PATCH {id}` (rename), `DELETE {id}`.

- **Ban** (`{u}/ban`, optional `{reason}` body) is GroupChat-only — a broadcast viewer can only be kicked, a direct chat has no moderation. Unlike a kick it is permanent: the banned row blocks rejoining.
- **Delete** (`DELETE {id}`) is a soft delete, Owner only; DirectChat rejects it in the aggregate.

Controllers (`Controllers/`): `ChatsController` (lifecycle + queries) + `ChatMembersController` (membership), both on `ApiControllerBase`; errors via global `DomainExceptionFilter` (`DomainException`→400, `ForbiddenException`→403, `ProblemDetails` + `traceId`); handlers/domain throw those semantic types (in `Chats.Domain`), **not** raw BCL exceptions — a framework `InvalidOperationException`/`ArgumentException` now → 500, not a mislabelled 400. Request records in `Contracts/`. See the `api_controllers` memory.
