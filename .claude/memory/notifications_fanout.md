---
name: notifications-fanout
description: Як Messaging створює нотифікації — через integration event MessageSentIntegrationEvent у RabbitMQ
metadata: 
  node_type: memory
  type: project
  originSessionId: 62cc2e6a-d21e-47f2-9647-8294fc3dff38
---

Fanout нотифікацій з Messaging у Notifications відбувається **асинхронно через RabbitMQ** (День 9, [[integration-events-rabbitmq]]).

**Потік:**
1. `SendMessageCommandHandler` викликає `messageRepository.AddAsync(message)`.
2. `MessageRepository.AddAsync` у Mongo-транзакції: зберігає документ + `IOutgoingEventsWriter.DispatchAsync(message.DomainEvents, session)`.
3. Dispatcher знаходить `MessageSentEventMapper`, мапить `MessageSentEvent` → `MessageSentIntegrationEvent`, серіалізує JSON і пише в outbox **у тій же транзакції**.
4. `OutgoingEventsSender` (BackgroundService у Infrastructure) кожні 2 сек тягне pending і публікує через `IPublishEndpoint`.
5. `MessageSentConsumer` (Notifications-сервіс, `Infrastructure/Messaging/Consumers`) приймає подію і викликає `mediator.Send(new FanoutChatNotificationCommand(...))`.
6. `FanoutChatNotificationCommandHandler` бере **recipients прямо з події** (embed у публікуючому контексті — після міграції Notifications не має доступу до `Chat`), пише через `AddManyIgnoringDuplicatesAsync` — **ідемпотентно** по `SourceEventId` (unique partial index `{RecipientId, SourceEventId}`), після чого публікує `UnreadCountChangedIntegrationEvent` **напряму** (у Notifications outbox-а нема — свідомо: подія сигнальна, UI робить signal-then-refetch).

**Why асинхронно:** атомарність save+publish без розподілених транзакцій, ізоляція fail-доменів (RabbitMQ down ≠ send fail), правильний DDD-розділ cross-context зв'язку. Synchronous `ISender.Send(FanoutChatNotificationCommand)` з `SendMessageCommandHandler` **видалений** на День 9.

**How to apply:**
- Якщо новий handler в іншому контексті повинен спричиняти нотифікації — НЕ викликати `FanoutChatNotificationCommand` прямо з handler. Замість цього: aggregate raise domain event → mapper → outbox → consumer → command.
- Потрібно додати: (1) domain event у aggregate, (2) `IIntegrationEventMapper` impl в Application, (3) реєстрація мапера у `AddOutgoingEvents` (per-service `InfrastructureSetup.cs`), (4) consumer у `Infrastructure/Messaging/Consumers/` сервісу-споживача, (5) реєстрація consumer'а через `bus.AddConsumer<T>()`.
- Repository, який зберігає aggregate з domain events, мусить дренувати їх через `IOutgoingEventsWriter` всередині транзакції. Це роблять `MessageRepository` (Messaging) і `ChatRepository` (Chats). Notifications/Presence публікують свої сигнальні/ефемерні події напряму без outbox — **свідомий виняток** (див. Eventing rules у кореневому CLAUDE.md).
- Для `NewMessage`/`MentionInGroup` `MessageId` обовʼязковий; для `MemberJoined`/`MemberKicked` — `null` (валідація у `NotificationPayload` factory методах).
