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
- Transactional Outbox у Mongo (колекція `outbox`)

**Why:** до Дня 9 cross-context fanout робився синхронним `ISender.Send(FanoutChatNotificationCommand)` всередині `SendMessageCommandHandler`. Це порушує DDD-розділ, блокує запит на час fanout, і Notifications fail вб'є send. Перехід на integration events дає атомарність save+publish (через outbox) і ізоляцію fail-доменів.

**Архітектура:**
- **Domain Events** (як було) — `MessageSentEvent`, `MemberJoinedEvent` тощо. Лежать у `aggregate.DomainEvents` після операції.
- **Integration Events** — POCO records у `src/TelegramLike.Contracts/` (на День 11 винесено з Application, бо їх будуть шарити між сервісами після міграції на microservices, [[microservices-migration]]). Зараз: `MessageSentIntegrationEvent` (Messaging), `MemberJoinedIntegrationEvent` + `MemberKickedIntegrationEvent` (Chats). Усі мають поле `Recipients: IReadOnlyList<Guid>` — обчислюється у публікуючому контексті, embed у event, щоб consumers не робили cross-context queries.
- **IIntegrationEventMapper** — інтерфейс в `Application/Common/IntegrationEvents/`. Один мапер на тип domain event. Реєструється як Singleton, dispatcher отримує `IEnumerable<IIntegrationEventMapper>` і будує `Dictionary<Type, IIntegrationEventMapper>`.
- **IDomainEventDispatcher** (internal у Infrastructure/Outbox) — приймає `IEnumerable<IDomainEvent>` + `IClientSessionHandle`, мапить, серіалізує (`System.Text.Json`), пише в outbox у тій же транзакції.
- **Outbox**: `outbox` Mongo-колекція + `IOutboxStore` (internal). Поля: `Id`, `EventType` (assembly-qualified), `Payload` (JSON), `OccurredAt`, `SentAt?`, `Retries`.
- **OutboxPublisherHostedService** — BackgroundService, кожні `Outbox:PollIntervalSeconds` сек тягне `SentAt == null && DeadLetteredAt == null`, deserialize по `Type.GetType(EventType)`, `IPublishEndpoint.Publish(payload, type)`, `MarkSentAsync`. На exception — `RecordFailureAsync(id, error, maxRetries)` (інкрементить Retries + пише LastError + ставить DeadLetteredAt коли досягло MaxRetries, Step 23).
- **Consumers** — у `Infrastructure/Messaging/Consumers/`. Тонкі: приймають integration event, викликають `IMediator.Send(<Command>)`. `MessageSentConsumer` викликає `FanoutChatNotificationCommand`.

**How to apply (новий integration event):**
1. Додати domain event у aggregate (якщо ще немає) — `aggregate.RaiseDomainEvent(new XEvent(...))`.
2. Створити integration event у `Application/<Context>/IntegrationEvents/XIntegrationEvent.cs` (implements `IIntegrationEvent`).
3. Створити мапер `XEventMapper : IIntegrationEventMapper` поруч.
4. Зареєструвати мапер у [DependencyInjection.cs](src/TelegramLike.Infrastructure/DependencyInjection.cs) в `AddOutbox`: `services.AddSingleton<IIntegrationEventMapper, XEventMapper>();`
5. Якщо потрібен consumer — створити в `Infrastructure/Messaging/Consumers/XConsumer.cs`, зареєструвати в `AddIntegrationMessaging` через `bus.AddConsumer<XConsumer>();`.
6. Repository, що зберігає aggregate з цим event, **мусить дренувати domain events у транзакції** через `IDomainEventDispatcher`. Зараз це роблять: `MessageRepository.AddAsync/UpdateAsync`, `ChatRepository.AddAsync/UpdateAsync` (з Дня 10). Для `NotificationRepository`/`UserPresenceRepository`/`UserRepository` треба додати при потребі.

**Семантика actor у fanout-командах:**
- `MemberJoined`: actor = joining user (він знає що приєднався, всі інші active members отримують нотифікацію).
- `MemberKicked`: actor = `KickedBy` (admin), бо адмін не повинен спам отримувати про власну дію. Кікнутий вже не ActiveMember, тому фільтр відсіє його автоматично — він не отримає "you were kicked" (поки що, окремий flow для цього не реалізований).

**Гарантії та обмеження:**
- Atomic save+outbox: так (Mongo транзакція).
- At-least-once delivery: так — якщо publish впав, повідомлення лишається `SentAt == null` і буде повторно опубліковано.
- Order: best-effort (sort by OccurredAt) — не строгий FIFO в межах consumer'а.
- Outbox-level DLQ (Step 23): poison message після `Outbox:MaxRetries` (default 5) переходить у `DeadLetteredAt != null` стан і виключається з `GetPendingAsync`. Replay поки ручний (clear `DeadLetteredAt` + reset `Retries` через Mongo shell). RabbitMQ-side DLQ/retry policies (MassTransit `UseDelayedRedelivery`) — НЕ налаштовано.

**Конфіги:**
- `RabbitMQ:Host/Username/Password/VirtualHost` у appsettings.json (у docker-compose — env vars `RabbitMQ__Host=rabbitmq`, `RabbitMQ__VirtualHost=telegramlike`). `VirtualHost` default — `/` для local dev; у docker — `telegramlike` (Step 26).
- `Outbox:PollIntervalSeconds` (default 2), `Outbox:BatchSize` (default 50), `Outbox:MaxRetries` (default 5, після нього → DLQ).

**Тести:**
- `MessageSentEventMapperTests` (Application.Tests) — unit на маппінг.
- `OutboxIntegrationTests` (Infrastructure.Tests) — Testcontainers Mongo, перевіряє: `AddAsync` пише в outbox в одній транзакції з messages; `MarkSentAsync` працює; `RecordFailureAsync` бампить counter без DLQ нижче ліміту; `RecordFailureAsync` перемикає у DLQ після `maxRetries` і виключає з pending.
- MassTransit test harness для consumer'ів — поки не додано. TODO якщо буде потрібен e2e тест.
