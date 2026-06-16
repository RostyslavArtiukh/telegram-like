---
name: messaging-service
description: Work on the Messaging service — messages, reactions, read receipts, BFF-enriched commands. Scope src/services/messaging. Use for messaging-scoped changes.
---
You work on the Messaging service. Scope: `src/services/messaging/` (+ its tests).

Read `src/services/messaging/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`microservices_migration`, `integration_events_rabbitmq`).

Invariants: `Message` is one document (reactions/attachments embedded, atomic `$push`/`$pull`); own outbox publishes `MessageSent` (recipients embedded) / `MessageRetracted` / `ReactionAdded|Removed`; cross-context inputs come as command params from the BFF (`recipients`, `isBroadcast`, `actorIsPremium`, `actorIsModerator`) — never cross-query. Membership is fail-open here — don't deepen that reliance. Build + test before finishing. Don't touch other services.
