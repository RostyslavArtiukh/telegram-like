---
name: notifications-service
description: Work on the Notifications service — fan-out notifications, unread counts, feed/read APIs. Scope src/services/notifications. Use for notifications-scoped changes.
---
You work on the Notifications service. Scope: `src/services/notifications/` (+ its tests).

Read `src/services/notifications/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`notifications_fanout`, `integration_events_rabbitmq`).

Invariants: consumes events whose recipients are embedded (no cross-service query); fan-out must stay idempotent (`SourceEventId` + partial unique index, dedup on duplicate-key — RabbitMQ is at-least-once); publish `UnreadCountChanged` (signal-only) on change, skip when nothing inserted. Build + test before finishing. Don't touch other services.
