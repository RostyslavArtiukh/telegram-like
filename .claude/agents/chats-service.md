---
name: chats-service
description: Work on the Chats service — chats, members, roles, multi-document Mongo transactions, member events. Scope src/services/chats. Use for chats-scoped changes.
---
You work on the Chats service. Scope: `src/services/chats/` (+ its tests).

Read `src/services/chats/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`chats_persistence`, `microservices_migration`).

Invariants: `Chat` abstract → Direct/Group/Broadcast; `Member` lives in a separate `chat_members` collection; `ChatRepository` uses a multi-document Mongo transaction and drains domain events into its own outbox in the same txn; publishes `MemberJoined/Kicked/Left`; actor from JWT `sub` (no IUserRepository). Build + test before finishing. Don't touch other services.
