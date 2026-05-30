# План розробки TelegramLike на 8 днів або більше

## День 1 (завтра, 5 травня 2026): Деталізація доменної моделі
- Додати агрегати, сутності, value objects для кожного bounded context
- Визначити події домену (domain events)
- Описати інваріанти та бізнес-правила

## День 2: Дизайн бази даних ✅
- PostgreSQL (основна БД) + Redis (ephemeral: typing, presence, sessions)
- ER-діаграма (Mermaid) — `docs/database.md`
- 13 таблиць, всі індекси та constraints описані

## День 3: Налаштування проекту
- Вибір технологій (Node.js + TypeScript, або .NET, або інше)
- Створення структури проекту
- Налаштування CI/CD

## День 4: Реалізація Identity контексту
- Реєстрація та авторизація користувачів
- Управління профілями
- Блокування користувачів

## День 5: Реалізація Chats контексту
- Створення чатів
- Управління учасниками та ролями
- Обробка FormerMember

## День 6: Реалізація Messaging контексту
- Надсилання повідомлень
- Реакції та read receipts
- Історія повідомлень

## День 7: Реалізація Presence та Notifications
- Online-статус та typing-індикатор
- Push-повідомлення
- Непрочитані повідомлення

## День 8: Тестування та деплоймент
- Інтеграційні тести
- UI/UX дизайн
- Деплоймент на хмарну платформу

## День 9 (2026-05-24): Integration Events через RabbitMQ ✅
- RabbitMQ у docker-compose (+ management UI на :15672)
- MassTransit (`MassTransit` + `MassTransit.RabbitMQ`)
- Transactional Outbox у Mongo (`outbox`-колекція)
- `OutboxDomainEventDispatcher`: дренує `aggregate.DomainEvents` після save, мапить у integration events, пише у outbox в **тій же транзакції**
- `OutboxPublisherHostedService` — BackgroundService що публікує pending через `IPublishEndpoint`
- `MessageSentIntegrationEvent` + `MessageSentConsumer` (у Notifications) замість синхронного `ISender.Send(FanoutChatNotificationCommand)`
- TODO: MassTransit test harness для consumer-тестів, retry policies, DLQ

## День 10 (2026-05-24): Member events fanout ✅
- `MemberJoinedIntegrationEvent` + `MemberKickedIntegrationEvent` + мапери
- `ChatRepository.AddAsync/UpdateAsync` тепер дренує `DomainEvents` через `IDomainEventDispatcher` у тій же транзакції (раніше це робив тільки `MessageRepository`)
- `MemberJoinedConsumer` / `MemberKickedConsumer` → `FanoutChatNotificationCommand`
- Семантика actor для Kick: `KickedBy` (admin) — кікнутий не отримує нотифікацію бо вже не ActiveMember
- 3 exchanges + 3 queues тепер у RabbitMQ
- TODO: інші domain events (MessageRetracted, ReactionAdded/Removed, MemberLeft/Banned/RoleChanged, ChatCreated/Renamed/Deleted, OwnershipTransferred, UserCame/WentOffline) — додавати в міру потреби consumers (SignalR, audit, email)

## День 11 (2026-05-24): Microservices prep — Notifications звільнено від cross-context dependency ✅
- Новий проект `src/TelegramLike.Contracts/` — POCO records для integration events (бо їх будуть шарити між сервісами)
- Embed `Recipients: IReadOnlyList<Guid>` у `MessageSentEvent`, `MemberJoinedEvent`, `MemberKickedEvent` (Domain)
- `Chat.RecipientsExcept(actorUserId)` helper у базовому Chat aggregate
- `Message.Send(...)` приймає `recipients` параметр; `SendMessageCommandHandler` обчислює з `chat.ActiveMembers`
- `FanoutChatNotificationCommand` тепер містить `Recipients: IReadOnlyList<Guid>` — НЕ викликає `IChatRepository`
- `FanoutChatNotificationCommandHandler` втратив залежність на `IChatRepository` — це підготовка до фізичного виділення Notifications-сервісу
- Consumers пробросують `Recipients` з integration event у command
- Day 12 буде: фізичне виділення Notifications у окремий сервіс з власною БД та API
- Day 13: Web стане BFF

## День 12 (2026-05-24): Notifications як окремий мікросервіс ✅ (об'єднано з Day 13)
**Структура:**
- Створено 4 нові проекти у `src/services/notifications/` (кожен сервіс — окрема підпапка, щоб масштабуватись):
  - `TelegramLike.Notifications.Domain` — Notification aggregate, VOs, events, IRepository (з власним Common/ — AggregateRoot/Entity/IDomainEvent дубльовано, бо service не повинен залежати від monolith Domain)
  - `TelegramLike.Notifications.Application` — commands, queries, FanoutChatNotificationCommandHandler, INotificationQueryService
  - `TelegramLike.Notifications.Infrastructure` — MongoNotificationRepository, MongoNotificationQueryService, MassTransit + 3 consumers (MessageSentConsumer/MemberJoinedConsumer/MemberKickedConsumer)
  - `TelegramLike.Notifications.Api` — ASP.NET Core minimal API, 4 endpoints
- 3 нові тестові проекти: Domain/Application/Infrastructure.Tests

**API endpoints (Notifications.Api на :8081):**
- `GET /notifications` — feed, query params `before`/`pageSize`/`unreadOnly`
- `GET /notifications/unread-count`
- `POST /notifications/{id}/read`
- `POST /notifications/read-all`
- Auth: header `X-User-Id` (внутрішня мережа, BFF — trust boundary)

**Власна БД:** Mongo `telegramlike_notifications` (окрема від monolith `telegramlike`)

**RabbitMQ:** консьюмери переїхали з monolith у Notifications.Infrastructure. Monolith тепер тільки публікує (через outbox). Спільний RabbitMQ broker.

**Web BFF:**
- `INotificationsApi` typed HttpClient + `NotificationsApiClient` (Web/Services/NotificationsApi/)
- `UserIdHeaderHandler` DelegatingHandler — пробросує `X-User-Id` з cookie auth
- Реєстрація: `AddHttpClient<INotificationsApi, NotificationsApiClient>().AddHttpMessageHandler<UserIdHeaderHandler>()`
- `appsettings.json`: `NotificationsApi:BaseUrl`; у docker `http://notifications:8080`
- Notifications.razor + NavMenu.razor → перейшли з `IMediator` на `INotificationsApi`

**Contracts розширено:**
- `NotificationApiDto`, `NotificationFeedApiDto`, `NotificationTypeContract`, `NotificationStatusContract` (POCO, для BFF контракту)

**Видалено з monolith:** `src/TelegramLike.Domain/Notifications/`, `src/TelegramLike.Application/Notifications/`, `Application/Common/Interfaces/INotificationQueryService.cs`, `Infrastructure/Persistence/MongoDB/Repositories/Notification*`, `Infrastructure/Messaging/Consumers/*`

**Результат:** Notifications живе у власному процесі/контейнері/БД. Зв'язок з monolith тільки через RabbitMQ (consume integration events) + HTTP (Web BFF). 102 тести зелені (48 + 22 + 15 monolith; 8 + 5 + 4 Notifications).

**TODO для прод:**
- ~~Auth: зараз `X-User-Id` довіряємо як є~~ ✅ виправлено День 14 (JWT)
- Distributed tracing (OpenTelemetry) — щоб бачити end-to-end request flow
- Health checks для Notifications.Api
- Окремий RabbitMQ vhost для notifications (зараз shared /)

## День 14 (2026-05-30): JWT auth між Web BFF і Notifications-сервісом ✅
- Замість довіри `X-User-Id` header — Web підписує короткоживучий JWT (HMAC-SHA256, 5 хв exp)
- Notifications валідує signature/issuer/audience через ASP.NET Core `AddJwtBearer` + `RequireAuthorization()` на `/notifications/*` group
- Shared secret у env (`ServiceAuth__JwtSecret` 384-bit base64), Issuer=`telegramlike-web`, Audience=`telegramlike-services`
- Новий `ServiceTokenIssuer` (Singleton) у Web; `UserIdHeaderHandler` перейменовано в `ServiceAuthHandler` — пробросує `Authorization: Bearer <jwt>` замість `X-User-Id`
- Сервіс читає userId з `sub` claim (`MapInboundClaims = false` щоб не маппилось у `nameidentifier`)
- `/health` endpoint лишається публічним (для compose healthcheck у майбутньому)
- Smoke verify: `GET /notifications/unread-count` без token → `401`, з валідним → `200`
- Цей паттерн готовий до reuse для наступних сервісів (Presence тощо) — той же `ServiceAuthHandler` працюватиме на будь-який downstream service
- TODO: окремі секрети per-environment (зараз hardcoded у appsettings.json — для прода винести у secret manager); rotation policy

## День 15 (2026-05-30): Presence як другий мікросервіс ✅
- Створено 4 проекти у `src/services/presence/` (Domain/Application/Infrastructure/Api) за тим самим паттерном що й Notifications
- Власна Mongo БД `telegramlike_presence` (колекція `user_presence`)
- Спільний Redis container (ключі вже namespaced: `presence:`, `typing:`)
- Власна ASP.NET Core minimal API на порту 8082: `POST /heartbeat`, `POST /offline`, `GET /{userId}`, `POST /typing/{chatId}/start|stop`, `GET /typing/{chatId}`
- **JWT auth re-used з Day 14** — той же `ServiceTokenIssuer`/`ServiceAuthHandler` у Web, той же `AddJwtBearer` config у Presence.Api (copy-paste pattern)
- Web BFF: `IPresenceApi` + `PresenceApiClient`, реєструється тим же `.AddHttpMessageHandler<ServiceAuthHandler>()` що й Notifications
- `UserIdKey` винесено з `NotificationsApiClient` на `ServiceAuthHandler` (shared option key)
- `MainLayout.razor` перейшов з `IMediator.Send(HeartbeatCommand)` на `IPresenceApi.HeartbeatAsync(userId)`
- **Cross-context dependency dropped:** `StartTypingCommandHandler` більше не викликає `IChatRepository.GetByIdAsync` (Presence-service не має доступу до Chats БД). Trust JWT-authenticated caller. TODO: local membership read-model коли додамо typing UI.
- Видалено з monolith: `src/TelegramLike.Domain/Presence/`, `src/TelegramLike.Application/Presence/` + `Common/Interfaces/IPresenceCache.cs`/`ITypingIndicatorService.cs`/`IUserPresenceQueryService.cs`, `Infrastructure/Caching/Redis/RedisPresence*.cs`/`RedisTyping*.cs`, `Infrastructure/Persistence/MongoDB/Repositories/UserPresence*.cs`
- 102 тести зелені (9 проектів: monolith 41+18+8; Notifications 8+5+4; Presence 7+4+7)
- Smoke OK: `curl POST /presence/heartbeat` без token → 401, з валідним → 204

**Що цей день довів:** паттерн з Day 12+14 reusable. Кожен новий сервіс ≈ година роботи (5 csproj move + Api shell з copy-paste JWT setup + BFF client за template + 1 razor edit).

**TODO:**
- Окрема Mongo per service vs shared instance з різними DB names — для прода краще окрема (зараз shared instance). Для pet ОК.
- Окремий Redis container для Presence — наразі shared. Якщо load зросте — split.
- Local membership read-model у Presence (subscribe на MemberJoined/Left/Kicked events) — щоб відновити strict typing-validation.

## День 17 (2026-05-30): Real-time typing через RabbitMQ + UX polish ✅
- **Push замість polling для typing:** `StartTypingCommandHandler` тепер додатково publish-ить `UserTypingIntegrationEvent` через MassTransit RabbitMQ (direct, без outbox — typing ephemeral). Web консьюмить через `UserTypingConsumer` → `ITypingPubSub.PublishAsync(chatId, userId)` → Razor компоненти підписані через `TypingPubSub.Subscribe(chatId, callback)` отримують виклик і роблять `InvokeAsync(StateHasChanged)`. Blazor circuit (вбудований SignalR) пушить UI оновлення у браузер.
- **Username замість GUID:** новий `GetUsernamesByIdsQuery` (Identity) + `IUserRepository.GetByIdsAsync` — `ChatView.razor` показує `Alice is typing…` замість `a3f2b1c4 is typing…`.
- **Typing indicator перенесений у chat header** — поруч з `active`/`online` лічильниками, не над input. Виглядає краще.
- **Batch presence endpoint:** `POST /presence/batch [ids]` → `{id: isOnline}`. `IPresenceApi.GetBatchPresenceAsync` замість N+1 викликів на presence checks.
- **Infrastructure DI:** `AddInfrastructure(IConfiguration, Action<IBusRegistrationConfigurator>?)` — Web передає `bus => bus.AddConsumer<UserTypingConsumer>()`, інші сервіси нічого не передають.
- 102 тести зелені, контейнери підняті, smoke OK (Presence публікує, Web консьюмить, UI оновлюється через Blazor circuit).

**Архітектурний висновок:** для real-time у Blazor Server **окремий SignalR Hub не потрібен** — Blazor circuit вже використовує SignalR під капотом. Достатньо in-memory pubsub (`ITypingPubSub`) щоб MassTransit consumer достукався до Razor компонента у тому ж процесі. Якщо в майбутньому Web масштабується горизонтально (декілька instance'ів) — pubsub перейде на Redis pub/sub або STAN.

**TODO:**
- Bump RabbitMQ keepalive для typing exchange (зараз durable за замовчуванням — overhead для ephemeral; можна `Durable=false`)
- Online polling замінити на push (UserCame/WentOnline integration events) — окремий день- Не показувати typing від себе у власному другому табі — фільтр `userId == _userId` (вже є)

## День 18 (2026-05-30): Auto-mark notifications as read for active chat ✅
- **UX bug fix:** badge notifications зростав навіть коли юзер активно у тому чаті де прийшло повідомлення.
- **Notifications.Domain:** `INotificationRepository.MarkAllForChatAsReadAsync(recipientId, chatId, readAt)`
- **Notifications.Application:** `MarkChatNotificationsAsReadCommand(RecipientId, ChatId)` + handler
- **Notifications.Infrastructure:** Mongo filter `RecipientId == x AND Payload.ChatId == y AND Status != Read` → bulk update
- **Notifications.Api:** `POST /notifications/chats/{chatId}/read` (auth required)
- **Web BFF:** `INotificationsApi.MarkChatAsReadAsync(userId, chatId)` + `NotificationsApiClient`
- **ChatView.razor:**
  - На `OnInitializedAsync` → виклик `MarkChatNotificationsReadAsync()`
  - У `ReloadMessagesAsync` → перевірка `grew = newCount > oldCount` → якщо так, теж викликає
- **NavMenu badge** оновлюється через 10 сек (як було) — досить швидко для UX

**TODO:**
- Push для unread-count (зараз 10-сек polling) — окремий integration event `NotificationReadIntegrationEvent` через RabbitMQ + pubsub в Web (як typing).