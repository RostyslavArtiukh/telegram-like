# Notifications service (port 8081, DB `telegramlike_notifications`)

Fan-out notifications + unread counts. Consumes integration events; exposes feed/read APIs. 4 projects, namespace `TelegramLike.Notifications.*`.

## Behaviour
- Consumes `MessageSent` / `MemberJoined` / `MemberKicked` → `FanoutChatNotificationCommand` → one `Notification` per recipient. **Recipients are embedded in the event** — no cross-service query.
- **Idempotent fan-out (do not remove):** `Notification.SourceEventId` + a unique partial index `{RecipientId, SourceEventId}`; `AddManyIgnoringDuplicatesAsync` swallows duplicate-key. RabbitMQ is at-least-once, so events can arrive twice.
- Publishes `UnreadCountChangedIntegrationEvent` (signal-only, no count value) on fanout / mark-read so the Web badge refetches. Skip publishing when nothing was inserted.

## Endpoints (`/notifications`, authed)
feed (`?before&pageSize&unreadOnly`), `unread-count`, `{id}/read`, `read-all`, `chats/{chatId}/read`.
