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

## Every integration event declares a wire name ([TL-117])
`[IntegrationEventName("context.event.v1")]` on the record — lowercase `context.event.vN`, kebab-case — is the event's identity. **The outbox stores that string, never the CLR name**, so a queued row no longer depends on the class keeping its name or namespace; rename or move the record freely, and only this string is the contract. Adding an event without the attribute fails `IntegrationEventNamesTests` in CI (and would throw at the first publish).

Two consequences worth knowing:
- **Never change a name once rows carry it.** A new shape gets a *new type* with `.v2`, per the rules below — editing the string strands every pending row exactly the way CLR names used to.
- **It doesn't make renames free on the wire.** MassTransit still routes by CLR type urn, so a rename still breaks *in-flight broker messages and consumers*; the wire name only fixes the *stored* outbox rows. `IntegrationEventNames.Resolve` also falls back to CLR resolution so rows written before [TL-117] still publish.

## Events that embed an audience are split into parts ([TL-124])
`MessageSent`, `MemberJoined`, `MemberKicked` and `ChatMembershipsSnapshot` embed who to reach so no consumer has to query another service. That embed made their size follow the size of the chat — one send into a 10k group was a single ~400 KB outbox row and broker frame. They now carry `PartIndex`/`PartCount` (additive, trailing, defaulting to part 0 of 1 so pre-[TL-124] payloads deserialize unchanged) and their list field holds **that part's slice**, not the whole audience.

Splitting happens in the publishing service's `IntegrationEventMap` via `FanoutParts.Split`. Each part is a complete event with its **own `EventId`** — deliberately, since Notifications deduplicates by `(recipient, source event)` and a shared id would look like a redelivery. **Writing a consumer: per-recipient work needs nothing; per-event work must gate on `PartIndex == 0`** (that is exactly what Realtime's chat-group push and Web's pubsub signal do).

## Versioning convention (when additive isn't enough)
MassTransit routes by the full type name/namespace and there is no schema registry, so a rename or field re-type is a wire break with no coexistence path. Rules:
1. **Additive first.** New optional data → a nullable trailing field with a default (e.g. `MemberJoinedIntegrationEvent.Role` in [TL-74b]). Old publishers/consumers keep working; consumers that care read the new field, others ignore it.
2. **Breaking change → a new versioned type in a `Vn` namespace** (`TelegramLike.Contracts.Chats.V2.MemberJoinedIntegrationEvent`), published *alongside* the old one until every consumer has migrated, then retire V1. Never mutate a shipped type's shape in place.
3. **`Realtime/RealtimeEvents.cs` is a frozen external surface** — shared verbatim with deployed SDK/MAUI clients that don't deploy in lockstep. Treat any change there as breaking; version it, don't edit it.
