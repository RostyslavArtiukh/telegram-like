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
2. `MessageRepository.AddAsync` у Mongo-транзакції: зберігає документ + `IDomainEventDispatcher.DispatchAsync(message.DomainEvents, session)`.
3. Dispatcher знаходить `MessageSentEventMapper`, мапить `MessageSentEvent` → `MessageSentIntegrationEvent`, серіалізує JSON і пише в outbox **у тій же транзакції**.
4. `OutboxPublisherHostedService` (BackgroundService у Infrastructure) кожні 2 сек тягне pending і публікує через `IPublishEndpoint`.
5. `MessageSentConsumer` (Infrastructure/Messaging/Consumers) приймає подію і викликає `mediator.Send(new FanoutChatNotificationCommand(...))`.
6. `FanoutChatNotificationCommandHandler` — як і раніше — бере `Chat`, фільтрує `ActiveMembers != actor`, робить `notificationRepository.AddManyAsync`.

**Why асинхронно:** атомарність save+publish без розподілених транзакцій, ізоляція fail-доменів (RabbitMQ down ≠ send fail), правильний DDD-розділ cross-context зв'язку. Synchronous `ISender.Send(FanoutChatNotificationCommand)` з `SendMessageCommandHandler` **видалений** на День 9.

**How to apply:**
- Якщо новий handler в іншому контексті повинен спричиняти нотифікації — НЕ викликати `FanoutChatNotificationCommand` прямо з handler. Замість цього: aggregate raise domain event → mapper → outbox → consumer → command.
- Потрібно додати: (1) domain event у aggregate, (2) `IIntegrationEventMapper` impl в Application, (3) реєстрація мапера у `AddOutbox` ([DependencyInjection.cs](src/TelegramLike.Infrastructure/DependencyInjection.cs)), (4) consumer у `Infrastructure/Messaging/Consumers/`, (5) реєстрація consumer'а через `bus.AddConsumer<T>()` у `AddIntegrationMessaging`.
- Repository, який зберігає aggregate з domain events, мусить дренувати їх через `IDomainEventDispatcher` всередині транзакції. Поки що це робить тільки `MessageRepository.AddAsync/UpdateAsync` — інші repositories (Chats, Notifications, Presence) ще ні. Додавати в міру потреби.
- Для `NewMessage`/`MentionInGroup` `MessageId` обовʼязковий; для `MemberJoined`/`MemberKicked` — `null` (валідація у `NotificationPayload` factory методах).
