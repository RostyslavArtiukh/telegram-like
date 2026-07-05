# TelegramLike.Contracts — shared published language

The **one shared project** every service and the Web BFF reference. It is NOT a service and holds **no domain or logic** — only POCO `record` DTOs that cross service boundaries. Zero dependencies.

## What's here (two kinds, grouped by owning context)
1. **Integration events** — RabbitMQ message schemas (`IIntegrationEvent`). Published by one service, consumed by others / the Web.
   - `Chats/` → `MemberJoined/Kicked/Left`
   - `Messaging/` → `MessageSent`, `MessageRetracted`, `ReactionAdded/Removed`
   - `Presence/` → `UserCameOnline/WentOffline/Typing`
   - `Notifications/` → `UnreadCountChanged`
2. **BFF API DTOs** — HTTP wire contracts between Web and a service (e.g. `Notifications/NotificationApiTypes.cs`).

## Why these folders exist even though the services were extracted
A context folder here is the **shared schema**, not the service's domain (that lives in `src/services/<name>/`). Publisher and consumer must share the exact same type — e.g. `MessageSentIntegrationEvent` is published by Messaging and consumed by both Notifications and Web. **Deleting a context folder breaks cross-service messaging/serialization** — don't remove one just because the service moved out.

## Rules
- POCO records only — no behaviour, no domain types, no package references.
- Changing an event/DTO shape is a **breaking change** for every publisher + consumer; evolve additively (nullable new fields) where possible.
- **Now public API — a shipped external app depends on this.** Since TL-64/66 the `TelegramLike.Client` SDK (and a MAUI build) reference these types, including `Realtime/RealtimeEvents.cs` (the SignalR push contracts, shared server↔client). Backend and client no longer deploy in lockstep, so treat every type here as versioned public surface: **additive-only** evolution, semver the SDK NuGet, and never rename/retype a field a deployed client reads.
- No `Identity/` folder: Identity publishes no integration events, and user data is fetched over HTTP into Web-local DTOs (`Web/Services/IdentityApi`).
