---
name: integration-events-rabbitmq
description: RabbitMQ + MassTransit + Transactional Outbox — як cross-context події публікуються між bounded contexts
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

День 9 (2026-05-24): додано event-driven cross-context зв'язок через RabbitMQ.

**Стек:**
- RabbitMQ 3 (management UI на http://localhost:15672, guest/guest)
- MassTransit 8.3 + MassTransit.RabbitMQ
- Transactional Outbox у Mongo (колекція `outgoing_events`)

**Why:** до Дня 9 cross-context fanout робився синхронним `ISender.Send(FanoutChatNotificationCommand)` всередині `SendMessageCommandHandler`. Це порушує DDD-розділ, блокує запит на час fanout, і Notifications fail вб'є send. Перехід на integration events дає атомарність save+publish (через outbox) і ізоляцію fail-доменів.

**Архітектура:**
- **Domain Events** (як було) — `MessageSentEvent`, `MemberJoinedEvent` тощо. Лежать у `aggregate.PendingEvents` після операції.
- **Integration Events** — POCO records у `src/TelegramLike.Contracts/` (на День 11 винесено з Application, бо їх будуть шарити між сервісами після міграції на microservices, [[microservices-migration]]). Повний набір (2026-07): Messaging — `MessageSent`, `MessageRetracted`, `ReactionAdded/Removed`; Chats — `MemberJoined/Left/Kicked`, `MemberRoleChanged`; Presence — `UserCameOnline/WentOffline`, `UserTyping`; Notifications — `UnreadCountChanged`. Стейтові події несуть `Recipients: IReadOnlyList<Guid>` — обчислюється у публікуючому контексті, embed у event, щоб consumers не робили cross-context queries.
- **IIntegrationEventMapper** — інтерфейс в `Application/Common/IntegrationEvents/`. Один мапер на тип domain event. Реєструється як Singleton, dispatcher отримує `IEnumerable<IIntegrationEventMapper>` і будує `Dictionary<Type, IIntegrationEventMapper>`.
- **IOutgoingEventsWriter** (internal у Infrastructure/Outbox) — приймає `IEnumerable<IChangeEvent>` + `IClientSessionHandle`, мапить, серіалізує (`System.Text.Json`), пише в outbox у тій же транзакції.
- **Outbox**: `outgoing_events` Mongo-колекція + `OutgoingEventsStore` (internal). Поля: `Id`, `EventType` (стабільне ім'я типу — не AssemblyQualifiedName, з [TL-75]), `Payload` (JSON), `OccurredAt`, `SentAt?`, `Retries`. З [TL-75] також claim/lease 60с — без дублювання publish під >1 репліку.
- **OutgoingEventsSender** — BackgroundService, кожні `OutgoingEvents:PollIntervalSeconds` сек тягне `SentAt == null && DeadLetteredAt == null`, deserialize по `Type.GetType(EventType)`, `IPublishEndpoint.Publish(payload, type)`, `MarkSentAsync`. На exception — `RecordFailureAsync(id, error, maxRetries)` (інкрементить Retries + пише LastError + ставить DeadLetteredAt коли досягло MaxRetries, Step 23).
- **Consumers** — у `Infrastructure/Messaging/Consumers/`. Тонкі: приймають integration event, викликають `IMediator.Send(<Command>)`. `MessageSentConsumer` викликає `FanoutChatNotificationCommand`.

**How to apply (новий integration event):**
1. Додати domain event у aggregate (якщо ще немає) — `aggregate.RecordEvent(new XEvent(...))`.
2. Створити integration event у `src/TelegramLike.Contracts/<Context>/XIntegrationEvent.cs`.
3. Створити мапер `XEventMapper : IIntegrationEventMapper` в Application сервісу-видавця.
4. Зареєструвати мапер у per-service `InfrastructureSetup.cs` в `AddOutgoingEvents`: `services.AddSingleton<IIntegrationEventMapper, XEventMapper>();`
5. Якщо потрібен consumer — створити в `Infrastructure/Messaging/Consumers/XConsumer.cs`, зареєструвати в `AddIntegrationMessaging` через `bus.AddConsumer<XConsumer>();`.
6. Repository, що зберігає aggregate з цим event, **мусить дренувати domain events у транзакції** через `IOutgoingEventsWriter`. Це роблять: `MessageRepository.AddAsync/UpdateAsync`, `ChatRepository.AddAsync/UpdateAsync` (з Дня 10). Notifications (`UnreadCountChanged`) і Presence (online/offline/typing) публікують **напряму без outbox — свідомий виняток** (сигнальні/ефемерні події, ідемпотентні консюмери; див. Eventing rules у кореневому CLAUDE.md). Identity подій не публікує.

**Семантика actor у fanout-командах:**
- `MemberJoined`: actor = joining user (він знає що приєднався, всі інші active members отримують нотифікацію).
- `MemberKicked`: actor = `KickedBy` (admin), бо адмін не повинен спам отримувати про власну дію. Кікнутий вже не ActiveMember, тому фільтр відсіє його автоматично — він не отримає "you were kicked" (поки що, окремий flow для цього не реалізований).

**⚠️ Queue naming — per-service префікс обов'язковий (2026-07-14):**
`AddRabbitMqBus(configuration, serviceName, registerConsumers)` — другий параметр ставить `KebabCaseEndpointNameFormatter(prefix: serviceName)`. Без нього дефолтний formatter іменує чергу за класом консюмера (`MemberJoinedConsumer` → `MemberJoined`), а presence/notifications/messaging всі мають однойменні консюмери → ТРИ сервіси конкурували за ОДНУ чергу, RabbitMQ роздавав кожну подію лише одному з них (round-robin) — read-моделі мовчки голодували (симптом: 403 "not an active member" у чаті, який щойно працював). Тепер черги `messaging-member-joined`, `presence-member-joined`, `notifications-member-joined` — кожен сервіс отримує свою копію (справжній pub/sub fanout). Realtime hub і Web BFF це не зачіпає — вони і так на per-instance тимчасових чергах (`InstanceId` + `Temporary`). Новий сервіс із консюмерами → передавай унікальний `serviceName`. Після деплою старі спільні черги видалено вручну (вони лишались прив'язаними і копили повідомлення).

**Гарантії та обмеження:**
- Atomic save+outbox: так (Mongo транзакція).
- At-least-once delivery: так — якщо publish впав, повідомлення лишається `SentAt == null` і буде повторно опубліковано.
- Order: best-effort (sort by OccurredAt) — не строгий FIFO в межах consumer'а.
- **Retention — mark-sent + TTL (2026-07-26, рішення юзера):** `MarkSentAsync` НЕ видаляє рядок, а ставить `SentAt`, тобто `outgoing_events` — це історія вже опублікованого, а не лише черга (юзер очікував видалення після відправки — це свідомий вибір, історія дає forensics типу «що і з яким лагом ми публікували»). Щоб не росла нескінченно, `OutgoingEventsIndexInitializer` створює TTL-індекс `sent_ttl` на `SentAt` з `OutgoingEvents:SentRetentionDays` (default 7). Pending і dead-lettered рядки TTL не чіпає НІКОЛИ — у них `SentAt: null`, а Mongo експайрить лише документи, де індексоване поле — BSON Date. Зміну retention на вже існуючому індексі робить `collMod`-фолбек: `CreateOneAsync` з іншим `expireAfterSeconds` кидає IndexOptionsConflict (code 85), і без фолбеку правка конфіга мовчки не діяла б.
- Outbox-level DLQ (Step 23): poison message після `OutgoingEvents:MaxRetries` (default 5) переходить у `DeadLetteredAt != null` стан і виключається з `GetPendingAsync`. Replay поки ручний (clear `DeadLetteredAt` + reset `Retries` через Mongo shell). RabbitMQ-side DLQ/retry policies (MassTransit `UseDelayedRedelivery`) — НЕ налаштовано.

**Конфіги:**
- `RabbitMQ:Host/Username/Password/VirtualHost` у appsettings.json (у docker-compose — env vars `RabbitMQ__Host=rabbitmq`, `RabbitMQ__VirtualHost=telegramlike`). `VirtualHost` default — `/` для local dev; у docker — `telegramlike` (Step 26).
- `OutgoingEvents:PollIntervalSeconds` (default 2), `OutgoingEvents:BatchSize` (default 50), `OutgoingEvents:MaxRetries` (default 5, після нього → DLQ), `OutgoingEvents:SentRetentionDays` (default 7 — TTL на опубліковані рядки). У appsettings жодного з них не задано — усі працюють на дефолтах.

**Тести:**
- `MessageSentEventMapperTests` (Application.Tests) — unit на маппінг.
- `OutboxIntegrationTests` (Infrastructure.Tests) — Testcontainers Mongo, перевіряє: `AddAsync` пише в outbox в одній транзакції з messages; `MarkSentAsync` працює; `RecordFailureAsync` бампить counter без DLQ нижче ліміту; `RecordFailureAsync` перемикає у DLQ після `maxRetries` і виключає з pending.
- MassTransit test harness для consumer'ів — поки не додано. TODO якщо буде потрібен e2e тест.
