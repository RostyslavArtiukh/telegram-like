---
name: telegramlike-project-status
description: "Поточний стан, план та ключові рішення по pet-проекту TelegramLike"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7cb0409c-41f8-48f6-a1f5-690d5ce7f4eb
---

Pet-проект — месенджер подібний до Telegram, розробляється з нуля.

> ⚠️ **ЦЕЙ ФАЙЛ ЗАСТАРІВ (нижче — стан на 2026-05-30, День 18, ще майже монолітний).** Актуальна архітектура — **мікросервіси + Blazor BFF + YARP gateway + client SDK + MAUI + realtime SignalR** — описана в кореневому `CLAUDE.md` та директорних `CLAUDE.md` (джерело істини). Прогрес міграції — [[microservices-migration]]; k8s — [[kubernetes-plan]]; SDK/MAUI — [[client-sdk-plan]]. Останній великий крок — наскрізний аудит якості й фікси **[TL-71..82]**, див. [[quality-audit-done]]. origin/master після цього. Нижній текст лишено для історії; **не довіряй його file:line/стеку без перевірки**.

**Поточний стан (2026-05-24):** Завершено Дні 1–9. День 9 — додано event-driven cross-context зв'язок через RabbitMQ + Transactional Outbox.

**Why:** Навчальний/практичний проект, мета — отримати досвід з DDD, real-time системами, мікросервісами.

**How to apply:** Всі архітектурні рішення повинні відповідати доменній моделі в `docs/domain.md`. Посилатись на неї при реалізації.

## 8-денний план
- День 1 (2026-05-05): Деталізація доменної моделі ✅
- День 2 (2026-05-05): Дизайн бази даних ✅ — MongoDB + Redis, 9 колекцій
- День 3: Налаштування проекту ✅ — .NET 9, Blazor Server, Clean Architecture
- День 4: Identity context ✅ — реєстрація, логін, BCrypt, Redis-сесії
- День 5 (2026-05-18): Chats context ✅ — Domain + Application + Infrastructure
- День 6 (2026-05-19): Messaging context ✅ — Domain + Application + Infrastructure
- День 7 (2026-05-23): Presence + Notifications ✅
- День 8 (2026-05-23): Тестування ✅ (91 тест), Blazor UI ✅, Docker deploy ✅ — 8-денний план виконано
- День 9 (2026-05-24): Integration Events через RabbitMQ ✅ — MassTransit + Transactional Outbox, [[integration-events-rabbitmq]]
- День 10 (2026-05-24): Member events fanout ✅ — `MemberJoined`/`MemberKicked` integration events + consumers; `ChatRepository` тепер дренує DomainEvents у outbox
- День 11 (2026-05-24): Microservices prep ✅ — окремий `TelegramLike.Contracts` проект; recipients embed у domain+integration events; Notifications звільнено від `IChatRepository` залежності, [[microservices-migration]]
- День 12 (2026-05-24): Notifications як окремий мікросервіс ✅ — 4 нових проекти у `src/services/notifications/`, власна БД `telegramlike_notifications`, ASP.NET Core Api на :8081, Web BFF з `INotificationsApi` HttpClient. Зв'язок з monolith тільки через RabbitMQ + HTTP, [[microservices-migration]]
- День 14 (2026-05-30): JWT auth між сервісами ✅ — Web підписує HMAC-SHA256 JWT (5хв exp), Notifications валідує через `AddJwtBearer` + `RequireAuthorization()`. Замість довіри `X-User-Id` header, [[service-auth-jwt]]
- День 15 (2026-05-30): Presence як другий мікросервіс ✅ — 4 проекти у `src/services/presence/`, власна БД `telegramlike_presence`, API на :8082, JWT auth reused з Day 14, `IPresenceApi` BFF, `MainLayout.razor` шле heartbeat по HTTP. Cross-context check у StartTyping прибрано (trust JWT caller), [[microservices-migration]]
- День 17 (2026-05-30): Real-time typing + UX polish ✅ — `UserTypingIntegrationEvent` через RabbitMQ → `UserTypingConsumer` у Web → `TypingPubSub` → Blazor circuit пушить UI. Username показ замість GUID (`GetUsernamesByIdsQuery`). Batch presence endpoint. Typing indicator у chat header, [[realtime-blazor-pubsub]]
- День 18 (2026-05-30): Auto-mark notifications as read for active chat ✅ — новий endpoint `POST /notifications/chats/{chatId}/read` + `MarkAllForChatAsReadAsync` у repo. ChatView викликає на init + при появі нових повідомлень. Прибрано UX-баг де badge зростав поки юзер у чаті.

## Docker deploy (День 8)
- `src/TelegramLike.Web/Dockerfile` — multi-stage build на `mcr.microsoft.com/dotnet/sdk:9.0` → `aspnet:9.0`. Слухає `:8080`.
- `.dockerignore` у корені (виключає bin/obj/.claude/.vs/docs/memory).
- `docker-compose.yml`: 3 сервіси — `mongodb` (replica set rs0, healthcheck `rs.initiate`), `redis`, `web` (depends_on mongodb healthy).
- Web env vars override `appsettings.json`: `MongoDB__ConnectionString=mongodb://mongodb:27017/?replicaSet=rs0&directConnection=true`, `Redis__ConnectionString=redis:6379`. У compose network DNS `mongodb`/`redis` резолвиться до контейнерів.
- `directConnection=true` залишений для compose теж — driver сам ігнорує advertised hostname з replica set і використовує DNS name.
- Запуск: `docker compose up -d --build` → http://localhost:8080.
- TODO для prod: DataProtection keys persisted to volume (зараз ефемерні — cookies anuluються при рестарті web).

## Ключові архітектурні рішення
- DDD: 5 Bounded Contexts: Identity, Chats, Messaging, Presence, Notifications
- **Стек:** .NET 9, Blazor Server, MediatR (CQRS), FluentValidation, MongoDB 7, Redis 7
- **Clean Architecture:** Web → Application → Domain; Infrastructure → Application
- **MongoDB запускається як single-node replica set** (`rs0`) — потрібно для multi-document транзакцій. Auth у локальному docker-compose вимкнено (pet-проект). [[chats-persistence]]
- BannedMember = стан у MemberStatus (не окремий агрегат)
- ForwardedMessage = поле ForwardReference в Message (не окрема сутність)
- Attachment = embedded array в documents `messages` (blob URL)
- Reactions = embedded array в documents `messages` (атомарний $push/$pull)
- TypingIndicator = Redis pub/sub + TTL 5 сек, не персистується в MongoDB
- OnlineStatus = MongoDB `user_presence` (джерело істини) + Redis кеш TTL 30 сек
- BroadcastChannel ReadReceipt = поле `broadcastReadCount` ($inc) в document `messages`
- chat_members = окрема колекція (не embedded в chats) — для масштабу великих груп
- blockedUserIds = embedded array в `users`
- Emoji set = 8 фіксованих (like, heart, laugh, wow, sad, angry, fire, dislike)
- PremiumStatus живе в Identity (не окремий білінг-контекст поки)
- Notifications.payload = flexible embedded object (різна форма залежно від type)

## Chats context (День 5)
- `Chat` — abstract base; sealed `DirectChat`, `GroupChat`, `BroadcastChannel`
- `Member` — Entity всередині `Chat`, але зберігається в окремій колекції `chat_members`
- `IChatRepository.AddAsync/UpdateAsync` — multi-document MongoDB транзакція (`IClientSessionHandle` + `WithTransactionAsync`)
- `Member` upsert у `UpdateAsync` через `BulkWrite + ReplaceOneModel{IsUpsert=true}` по `Member.Id` (статуси Left/Kicked/Banned не видаляють запис)
- Queries окремо: `IChatQueryService` повертає DTO (`ChatSummaryDto`, `ChatDetailsDto`, `ChatMemberDto`)
- Actor (виконавець) приходить параметром у команду (немає `ICurrentUserAccessor` поки)
- BroadcastChannel: `Join` → роль `Viewer`; роль міняється через `PromoteToAdmin`/`DemoteToViewer` (не довільний ChangeRole)
- DirectChat: `Rename`/`Delete`/`Leave`/`Kick` кидають `InvalidOperationException`

## Messaging context (День 6)
- `Message` — aggregate root; `Reaction` — entity всередині
- VOs: `MessageContent` (text + attachments, ≥1 not-empty), `Attachment`, `ReplyReference`, `ForwardReference`, `MessageStatus`, `Emoji` (enum), `AttachmentType`
- `IMessageRepository` — простий CRUD на колекції `messages` (без транзакцій — агрегат у одному документі)
- Cross-context перевірки в **Application handlers**: `SendMessageCommandHandler` тягне `Chat` через `IChatRepository`, валідує `ActiveMember` + `Broadcast → Owner/Admin only` + reply target в тому ж чаті, не retracted
- `RetractMessage`: автор або (Admin/Owner у Chat). Soft-delete: `IsRetracted=true`, `Content` → `[retracted]`
- `AddReaction`: ліміт 1 (2 для Premium через `IUserRepository.GetByIdAsync` → `user.IsPremium`)
- `MarkMessageAsRead`: для `Direct/Group` → `message_read_receipts`; для `Broadcast` → `$inc broadcastReadCount` на самому документі. Self-read пропускається.
- `HideMessage` — read-model `hidden_messages` (upsert на `{messageId, userId}`)
- Query `GetChatMessages` — keyset-пагінація по `SentAt` DESC (PageSize+1 для NextCursor), фільтрація `hidden_messages` зробити в `MessageQueryService`
- DTOs у `Application/Messaging/Queries/MessageDtos.cs`
- `SendMessageCommandHandler` тепер також викликає `ISender.Send(FanoutChatNotificationCommand)` після успішного збереження повідомлення [[notifications-fanout]]

## Presence context (День 7)
- `UserPresence` — aggregate root з полями `Status` (Online/Offline), `LastSeenAt`, `HideLastSeen`. `Id` = `UserId`.
- `GoOnline` / `GoOffline` методи; при offline `LastSeenAt` зануляється, якщо `HideLastSeen=true`. Events: `UserCameOnlineEvent`, `UserWentOfflineEvent`.
- `IUserPresenceRepository` — upsert у Mongo колекцію `user_presence` (просто `ReplaceOneAsync{IsUpsert=true}` без транзакцій).
- `IPresenceCache` (Redis): `TouchAsync`/`IsOnlineAsync`/`ClearAsync` через ключ `presence:{userId}` з TTL 30 сек (з конфігу `Presence:HeartbeatTtlSeconds`).
- `ITypingIndicatorService` (Redis): ключ `typing:{chatId}:{userId}` з TTL 5 сек (`Presence:TypingTtlSeconds`). `GetTypingUserIdsAsync` — через `server.KeysAsync(pattern: "typing:{chatId}:*")`.
- Commands: `HeartbeatCommand` (Touch Redis + перевести Mongo у Online якщо треба), `GoOfflineCommand`, `StartTypingCommand` (перевіряє ActiveMember у Chat), `StopTypingCommand`.
- Queries: `GetUserPresenceQuery` (поєднує Mongo-док з live Redis-перевіркою — Redis авторитативний для "online зараз"), `GetTypingUsersQuery`.

## Notifications context (День 7)
- `Notification` — aggregate root, immutable після створення (змінюється тільки `Status`/`ReadAt`). Методи: `Create` (factory) / `MarkAsDelivered` / `MarkAsRead`.
- VOs: `NotificationType` enum (NewMessage/MentionInGroup/MemberJoined/MemberKicked), `NotificationStatus` enum, `NotificationPayload` з factory-методами (`ForNewMessage`, `ForMention`, `ForMemberJoined`, `ForMemberKicked`).
- `INotificationRepository`: `AddAsync`, `AddManyAsync` (`InsertManyAsync` для fanout), `UpdateAsync`, `MarkAllAsReadAsync` (single `UpdateManyAsync` зі статусом != Read).
- Колекція `notifications` з embedded `NotificationPayloadDocument` (flexible: `MessageId`/`ActorId` мають `[BsonIgnoreIfNull]`).
- Commands: `MarkNotificationAsReadCommand` (перевірка що recipient == requesting user), `MarkAllNotificationsAsReadCommand`, `FanoutChatNotificationCommand` (internal — про нього нижче).
- Queries: `GetNotificationFeedQuery` (keyset-пагінація по `CreatedAt` DESC, опція `UnreadOnly`), `GetUnreadCountQuery` (`CountDocumentsAsync`). DTOs у `Application/Notifications/Queries/NotificationDtos.cs`.

## Notifications fanout (День 7 → переписано на День 9)
- `FanoutChatNotificationCommand` — internal MediatR-команда; **виклик з SendMessageCommandHandler видалений на День 9**.
- Тепер fanout трігериться через `MessageSentIntegrationEvent` у RabbitMQ → `MessageSentConsumer` → `mediator.Send(FanoutChatNotificationCommand)`. Деталі в [[integration-events-rabbitmq]] і [[notifications-fanout]].
- `FanoutChatNotificationCommandHandler` (без змін) читає `Chat`, бере `chat.ActiveMembers.Where(m => m.UserId != actorId)`, створює `Notification` для кожного через `Notification.Create()` і робить `INotificationRepository.AddManyAsync`.

## День 9: Integration Events через RabbitMQ
- docker-compose: додано `rabbitmq:3-management` (5672, 15672), healthcheck `rabbitmq-diagnostics ping`.
- NuGet (Infrastructure): `MassTransit` 8.3 + `MassTransit.RabbitMQ` + Hosting/Logging abstractions.
- **Outbox** у Mongo: колекція `outgoing_events` з полями `{Id, EventType, Payload (JSON), OccurredAt, SentAt?, Retries}`.
- `MessageRepository.AddAsync/UpdateAsync` тепер у Mongo-транзакції (`WithTransactionAsync`): save messages + `IOutgoingEventsWriter.DispatchAsync(events, session)` → atomic.
- `OutboxDomainEventDispatcher`: `Dictionary<Type, IIntegrationEventMapper>`, серіалізує через `System.Text.Json`, записує batch в outbox.
- `OutgoingEventsSender` (BackgroundService): кожні `OutgoingEvents:PollIntervalSeconds` (default 2с) тягне `SentAt == null`, `Type.GetType(EventType)` → deserialize → `IPublishEndpoint.Publish(payload, type)` → `MarkSentAsync`. На fail — `IncrementRetryAsync` + log.
- `MessageSentConsumer` (Infrastructure/Messaging/Consumers) — `IConsumer<MessageSentIntegrationEvent>`, делегує `IMediator.Send(FanoutChatNotificationCommand)`.
- DI: `AddOutgoingEvents` (mappers, store, dispatcher, hosted service) + `AddIntegrationMessaging` (`AddMassTransit` з `UsingRabbitMq`, `ConfigureEndpoints`).
- Тести: `MessageSentEventMapperTests` (unit) + `OutboxIntegrationTests` (Testcontainers Mongo, 3 тести).

## Файли
- `docs/domain.md` — повна доменна модель (агрегати, entities, VO, events, інваріанти)
- `docs/database.md` — схема MongoDB (9 колекцій, індекси, Redis-ключі)
- `docs/plan.md` — 8-денний план
- `docs/stack.md` — стек, NuGet-пакети, структура solution
