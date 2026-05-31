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

## День 20 (2026-05-30): Real-time messages — push замість 3-сек polling ✅
- Той самий patern що typing з Day 17. Web додає другий consumer `NewMessageConsumer : IConsumer<MessageSentIntegrationEvent>` → `INewMessagePubSub.PublishAsync(chatId, messageId)` → ChatView підписки fire → `ReloadMessagesAsync()`.
- `MessageSentIntegrationEvent` вже публікується monolith через outbox (Day 9). Notifications service та Web тепер обидва підписуються (різні MassTransit consumer queues, обидва отримують fanout від exchange).
- ChatView 3-сек poller тепер тільки `RefreshPresenceAsync()` + `SweepExpiredTyping()` — message reload видалено з нього.
- **Latency:** від send → other browser sees message ≈ outbox poll 2с + RabbitMQ → consumer → pubsub → Blazor circuit ≈ 2.5с (раніше 3с polling worst-case).

**TODO:**
- Real-time для `MessageRetractedIntegrationEvent` + `ReactionAdded` — той самий patern.
- Optimistic UI: показувати власне повідомлення одразу після send без чекати реальний event back.

## День 21 (2026-05-30): Real-time NavMenu unread badge ✅
- Останній polling-точка прибрана. NavMenu badge тепер push.
- **Новий event у Contracts:** `UnreadCountChangedIntegrationEvent(EventId, OccurredAt, UserIds: Guid[])` — signal-only payload (без count value, щоб уникнути stale-read race між конкурентними операціями).
- **Notifications service publishes з 4 handlers:** `FanoutChatNotificationCommandHandler` (UserIds = recipients), `MarkNotificationAsReadCommandHandler` / `MarkAllNotificationsAsReadCommandHandler` / `MarkChatNotificationsAsReadCommandHandler` (UserIds = [actor]).
- **Web:** `IUnreadCountPubSub` + `UnreadCountChangedConsumer` за тим самим patern як typing/messages. Consumer iterates UserIds → publish per-user у pubsub.
- **NavMenu.razor:** замість `Timer` тепер `Subscribe(_userId, OnUnreadChangedAsync)` → refetch через `INotificationsApi.GetUnreadCountAsync` → `StateHasChanged`. Polling Timer видалений.
- Цей patern третій раз reused (typing/messages/unread-count) — підтверджує що `IXPubSub` + `IConsumer` + RabbitMQ події = working real-time для Blazor Server без окремого SignalR Hub.

## Step 22 (2026-05-31): Notification fanout — ідемпотентність ✅
- **Bug clousure:** RabbitMQ at-least-once + outbox-publisher retry → одна `MessageSentIntegrationEvent` могла бути доставлена двічі → у юзера створювалось 2 Notification документи + badge стрибав на +2. Тепер консьюмери ідемпотентні.
- **Domain:** `Notification` отримав поле `SourceEventId` (з валідацією `≠ Guid.Empty`); `Create(...)` приймає його 4-м параметром.
- **Application:** `FanoutChatNotificationCommand` має `SourceEventId`; усі 3 консьюмери (`MessageSentConsumer` / `MemberJoinedConsumer` / `MemberKickedConsumer`) пробросують `context.Message.EventId` як source-id.
- **Infrastructure:**
  - `INotificationRepository.AddManyIgnoringDuplicatesAsync(...)` — `InsertManyAsync` з `IsOrdered=false`, ловить `MongoBulkWriteException`, рахує duplicate-key (code 11000) як "вже вставлено", решту прокидає.
  - `NotificationIndexInitializer : IHostedService` на старті створює unique compound index `{RecipientId, SourceEventId}` з `PartialFilterExpression: Exists("SourceEventId")` — partial щоб legacy документи без поля не падали.
- **Handler:** якщо всі notifications виявились дублями (`inserted == 0`) — не публікує `UnreadCountChangedIntegrationEvent` (нічого не змінилось, нащо будити UI).
- **Tests:** +2 domain tests (empty source-event-id throws, source-event-id persists), +1 application test (skips publish при 0 inserted). Інтеграційний тест на duplicate-insert не доданий (потребує Docker, можна додати наступним кроком).
- **Що НЕ змінилось:** existing data не торкнули — partial index ігнорує old rows. Перший relaunch створює індекс, далі — захист працює.
- **Naming:** перший запис під новим "Step N" іменуванням (замість "Day N"), див. memory `nomenclature_step_not_day.md`.


**Наслідок:** усі UI-критичні події тепер push: typing, нові повідомлення, unread count. Polling залишився тільки у `Notifications.razor` (3 сек page state) і `RefreshPresenceAsync` у ChatView (3 сек online dots). Обидва теж можна push-ити при потребі.

## Step 23 (2026-05-31): Outbox — DLQ + max retries ✅
- **Тех-борг:** `OutboxPublisherHostedService` робив `IncrementRetryAsync` на будь-який fail без верхньої межі — poison message (bad payload, unresolvable EventType, постійний broker reject) міг крутитись нескінченно і займати слот у кожному batch.
- **Options:** `OutboxPublisherOptions.MaxRetries` (default 5, перевизначається через `Outbox:MaxRetries`).
- **Store API:**
  - `IncrementRetryAsync(id)` замінено на `RecordFailureAsync(id, error, maxRetries)` — у одній операції інкрементить `Retries`, пише `LastError`, і якщо `Retries >= maxRetries` ставить `DeadLetteredAt = UtcNow` другим update'ом.
  - `GetPendingAsync` тепер фільтрує `SentAt == null && DeadLetteredAt == null` — DLQ-документи не повертаються більше.
  - Новий `GetDeadLetteredAsync(batchSize)` для майбутньої operability (UI/CLI для replay).
- **Document:** `OutboxDocument` отримав поля `DeadLetteredAt: DateTime?`, `LastError: string?` (обидва `[BsonIgnoreIfNull]` — non-breaking для існуючих документів).
- **Logging:** publisher логує `LogWarning` для звичайного retry і `LogError` коли message переходить у DLQ.
- **Tests:** +2 integration tests у `OutboxIntegrationTests` (один на retry counter без DLQ, інший на DLQ flip після `maxRetries`). Старий `IncrementRetryAsync_bumps_counter` замінено на нові. 107/107 passing.

**TODO:**
- Endpoint/CLI для replay DLQ messages (manual ack: clear `DeadLetteredAt` + reset `Retries`) — поки операція ручна через Mongo shell.
- Той самий patern для consumers у Notifications (RabbitMQ has own DLX, але app-level fail handling теж знадобиться).

## Step 24 (2026-05-31): Health checks + compose `depends_on: service_healthy` ✅
- **Тех-борг:** `web` сервіс мав `depends_on: notifications/presence: condition: service_started` — стартував коли downstream-процеси тільки відкрились на порту, але не обов'язково готові обслуговувати. На холодному старті compose-стеку перші запити з UI летіли у не-ready сервіс і отримували 5xx, поки той ще під'єднувався до Mongo/RabbitMQ/Redis.
- **Apis (Notifications + Presence):**
  - NuGet: `AspNetCore.HealthChecks.MongoDb` 9.0.0, `AspNetCore.HealthChecks.Redis` 9.0.0 (тільки Presence).
  - **Чому НЕ `AspNetCore.HealthChecks.Rabbitmq`:** ця бібліотека у 9.x тягне `RabbitMQ.Client 7.x` (async API), а MassTransit 8.3 побудований на `RabbitMQ.Client 6.x` (sync) — на старті падає `MissingMethodException: ConnectionFactory.CreateConnection(IList<string>, string)`. **MassTransit сам авто-реєструє `masstransit-bus` health check з тегом `"ready"`**, тому ніяких додаткових пакетів не треба.
  - Wiring у Program.cs: `AddHealthChecks().AddMongoDb(...).AddRedis(...)` з тегом `"ready"`.
  - Два endpoints: `/health/live` (predicate `_ => false` — лише доводить що pipeline жива) і `/health/ready` (`Tags.Contains("ready")` — Mongo + RabbitMQ + Redis якщо є). Legacy `/health` лишився щоб старі smoke-скрипти не зламались.
- **Dockerfiles:** `apt-get install curl` у final image (потрібен для compose healthcheck CMD).
- **docker-compose.yml:**
  - `notifications` і `presence` отримали `healthcheck: curl -fsS http://localhost:8080/health/ready` (interval 10s, retries 5, start_period 20s).
  - `web.depends_on.notifications/presence` тепер `condition: service_healthy` (раніше `service_started`).
- **Smoke verify:** `docker compose up -d notifications presence` → дочекатись `healthy` → `curl http://localhost:8081/health/ready` повертає `Healthy`, те саме для :8082. `web` стартує тільки коли обидва сервіси `healthy`.
- **Тести:** 107/107 (нічого не зламалося, нових тестів не додано — perf/health checks — runtime-only поведінка, юніт-тести не дають value).

**TODO:**
- Healthcheck для `web` (Blazor) — можна додати окремий aspnet HealthChecks UI для observability на одній сторінці. Зараз `web` стартує без depends_on healthcheck на самого себе (це і не треба).
- Окремий liveness vs readiness у kubernetes-стилі: liveness restart pod якщо процес завис; readiness виключає з load balancer. У docker-compose різниці немає, але код вже готовий до k8s.

## Step 25 (2026-05-31): Local membership read-model у Presence ✅
- **Тех-борг (з Day 15):** при винесенні Presence у окремий сервіс прибрали `IChatRepository.GetByIdAsync` виклик у `StartTypingCommandHandler` (Presence БД не має таблиці чатів). Замість справжньої membership-валідації — trust JWT-authenticated caller. Тепер відновлено через event-driven локальну read-модель.
- **Contracts:** новий `MemberLeftIntegrationEvent(EventId, OccurredAt, ChatId, UserId)`. У monolith додано mapper `MemberLeftEventMapper` + registered у `AddOutbox` — domain event `MemberLeftEvent` тепер дренує у outbox як інші membership-події.
- **Presence.Application:** `IChatMembershipReadModel` interface з 3 операціями: `IsActiveMemberAsync`, `UpsertActiveAsync`, `RemoveAsync`.
- **Presence.Infrastructure:**
  - `MongoChatMembershipReadModel` — колекція `chat_memberships` у `telegramlike_presence`. Document Id = `"{chatId:N}:{userId:N}"` (composite key — природня унікальність + idempotent upserts без окремого індексу).
  - 3 consumers (`MemberJoinedConsumer` / `MemberKickedConsumer` / `MemberLeftConsumer`) — тонкі pass-through до read-model. Зареєстровані у `AddMassTransit(bus => bus.AddConsumer<>())` + `ConfigureEndpoints` (раніше у Presence ConfigureEndpoints не викликався — лише publish-only).
- **StartTypingCommandHandler:** перед `typingService.StartTypingAsync` запитує `IChatMembershipReadModel.IsActiveMemberAsync`. **Fail-open поведінка:** якщо read-model не знає про пару — лог warning і пропускає (бо існуючі чати створені до цієї фічі НЕ у read-model). Це навмисний тимчасовий компроміс — закоментовано у коді, plan-ується замінити на fail-closed коли буде backfill.
- **Tests:** +6 integration tests у Presence.Infrastructure (`ChatMembershipReadModelIntegrationTests`) — Mongo Testcontainers, upsert/remove/idempotency/isolation. +2 unit у Presence.Application (`StartTypingCommandHandlerTests`) — active member path і fail-open warning path. 115/115 pass (поспіл з Step 23: 107).
- **Side fix:** `Testcontainers.Redis` bump 3.10.0 → 4.12.0 через сумісність із новим `Testcontainers.MongoDb` 4.12.0 (старі major-version core несумісні).

**Гарантії:**
- Presence сервіс лишається **autonomous** — не має доступу до Chats БД. Read-model це local materialized view на свої події з RabbitMQ.
- **Eventually consistent:** є вікно ~outbox poll (2с) + RabbitMQ delivery між Join і фактичним відбиттям у read-model. Для UX typing — несуттєво.
- **Idempotent:** RabbitMQ at-least-once → upsert повторно безпечний (composite key); remove повторно — no-op.

**TODO:**
- Backfill: запустити одноразовий job який читає `chat_members` з Chats БД і насіє read-model. Тоді можна tighten StartTyping до fail-closed (`throw new InvalidOperationException` для non-members) — це справжня security boundary.
- Опціонально: подібну read-model можна реюзати у Notifications/інших майбутніх сервісах якщо їм треба знати membership.

## Step 26 (2026-05-31): Shared `telegramlike` RabbitMQ vhost ✅
- **Тех-борг (cosmetic):** усі MassTransit DI використовували hardcoded `"/"` (дефолтний RabbitMQ vhost). Якщо broker shared з іншим app — exchanges/queues конфліктували б у management UI; нічого не відділяло TelegramLike топологію.
- **Що НЕ робили (і чому):** справжній vhost-per-service (`/notifications`, `/presence` тощо) ламає cross-service routing — RabbitMQ не маршрутизує між vhost'ами без `rabbitmq-shovel` чи `rabbitmq-federation` plugins. Це M+ робота, не cosmetic. Свідомо обмежились до namespace-ізоляції на рівні всієї системи.
- **DI patern:** у всіх 3 `AddIntegrationMessaging` (`Infrastructure`, `Notifications.Infrastructure`, `Presence.Infrastructure`) додано `var vhost = configuration["RabbitMQ:VirtualHost"] ?? "/"` + `cfg.Host(host, vhost, ...)`. Дефолт `/` зберігає local `dotnet run` working без compose.
- **docker-compose:**
  - `rabbitmq.environment.RABBITMQ_DEFAULT_VHOST: telegramlike` — RabbitMQ створить vhost при першому старті і guest-юзер автоматично отримає до нього доступ.
  - Усі 3 сервіси (`web`, `notifications`, `presence`) отримали `RabbitMQ__VirtualHost: telegramlike`.
- **Verify:** `docker exec telegramlike-rabbitmq rabbitmqctl list_vhosts` → тільки `telegramlike` (дефолтний `/` не існує бо `RABBITMQ_DEFAULT_VHOST` його замінив). `list_exchanges -p telegramlike` показує всі наші exchanges (`MessageSent`, `MemberJoined/Kicked/Left`, `TelegramLike.Contracts.*`). 115/115 тестів proходять (Testcontainers Rabbit/Mongo не зачеплені).

**TODO:**
- Якщо колись захочемо реальної per-service ізоляції — `rabbitmq-shovel` plugin із static config який forward-ить cross-service events між vhost'ами. Або federation з upstream/downstream. Не зараз.

## Step 27 (2026-05-31): OpenTelemetry tracing → Jaeger ✅
- **Тех-борг:** не було способу побачити end-to-end flow Web → Notifications/Presence — лог-кореляція ручна (greps по timestamp). Тепер кожен HTTP-запит і кожна RabbitMQ-подія мають traceId, що пов'язує всі spans в одну стрічку.
- **Інструментація:**
  - `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.AspNetCore` (incoming HTTP) + `OpenTelemetry.Instrumentation.Http` (outgoing HttpClient у Web) + `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP/gRPC).
  - `AddSource("MassTransit")` — MassTransit має свій `ActivitySource`, тому publish/consume автоматично потрапляють у трейс. Trace context (traceparent) injects/extracts через RabbitMQ message headers.
  - **Не покрито:** MongoDB.Driver і StackExchange.Redis spans — потребують окремих instrumentation packages. Перші big-picture spans важливіші, додам пізніше якщо буде потреба.
- **Resource attributes:** `service.name` = `telegramlike.web` / `.notifications` / `.presence`; `service.version` з assembly version.
- **OTLP endpoint:** конфіг `Tracing:OtlpEndpoint`. Якщо порожній — exporter не реєструється (silent no-op для `dotnet run` без compose). У docker — `http://jaeger:4317`.
- **docker-compose:**
  - Новий сервіс `jaeger` — `jaegertracing/all-in-one:1.60`. Ports `16686` (UI), `4317` (OTLP gRPC). Memory-only storage. `COLLECTOR_OTLP_ENABLED=true` щоб приймати OTLP (раніше було тільки Jaeger native protocol).
  - Web/Notifications/Presence отримали `Tracing__OtlpEndpoint=http://jaeger:4317`.
  - **Notice:** спочатку взяв тег `1.62`, але такого нема в Docker Hub — actual latest 1.x branch це `1.60` (далі Jaeger перейшов на v2 з іншою CLI). Зафіксував `1.60`.
- **Smoke verify:** `docker compose up -d` → всі сервіси healthy → `curl http://localhost:16686/api/services` повертає `["telegramlike.web", "telegramlike.notifications", "telegramlike.presence"]`. ASP.NET Core spans з повним набором тегів (`http.request.method`, `http.response.status_code`, `url.path`, `network.protocol.version`).
- **Що побачите при реальному flow:** заходимо у Web → відкриваємо чат → надсилаємо повідомлення → у Jaeger UI: span "POST /chats/.../send" (Web) → child "INSERT messages" (Mongo, як додамо) → "outbox.publish MessageSentIntegrationEvent" (MassTransit) → через RabbitMQ → "consume MessageSentIntegrationEvent" (Notifications) → child "INSERT notifications". Усе одна traceID.
- **Тести:** 115/115 (OTel не зачіпає тестову поверхню — exporter NoOp у тестах бо `Tracing:OtlpEndpoint` не виставлено).

**TODO:**
- Mongo + Redis instrumentation (`MongoDB.Driver.Core.Extensions.DiagnosticSources` + `OpenTelemetry.Instrumentation.StackExchangeRedis`). Дадуть DB-level spans з query times, дуже корисно для perf.
- Sampling policy: зараз 100% trace rate (always-on). Для prod треба `TraceIdRatioBased(0.1)` або head-based sampling щоб не залити Jaeger.
- Metrics + Logs через OTel (зараз тільки traces). Той самий exporter може шлити три типи signal-ів.

## Step 28 (2026-05-31): Real-time для MessageRetracted + ReactionAdded/Removed ✅
- **Тех-борг:** після Day 20 нові повідомлення приходили push'ем, але retract і reactions ChatView не бачив поки користувач не оновив сторінку — UI випадав із "живого" feel що з'явився для send.
- **Patern:** третій раз reused (Day 17 typing, Day 20 new-message, Day 21 unread; Day 25 теж re-used як read-model але інший use-case). `IXPubSub` + RabbitMQ integration event + Web consumer + Razor subscription. Тепер з ним стало ясно що оптимальна форма — **один pubsub на UI-action**, а не на event-type: ChatView потребує один callback (reload), не три.
- **Contracts:** 3 нових integration events (`MessageRetractedIntegrationEvent`, `ReactionAddedIntegrationEvent`, `ReactionRemovedIntegrationEvent`) — без `Recipients`, бо ці події УI-only (Web їх consume-ить, fanout не потрібен).
- **Application:** 3 mappers (`MessageRetractedEventMapper`, `ReactionAddedEventMapper`, `ReactionRemovedEventMapper`). Зареєстровані у `AddOutbox` → `MessageRepository.UpdateAsync` тепер дренує всі ці події через outbox автоматично (вже raise-лись у `Message.Retract`/`AddReaction`/`RemoveReaction` ще з Day 6).
- **Web (нова папка `Services/ChatChanged/`):** `IChatChangedPubSub` (Subscribe(chatId, Func<Task>) + PublishAsync(chatId)) + impl + 3 thin consumers (`MessageRetractedConsumer`, `ReactionAddedConsumer`, `ReactionRemovedConsumer`) — всі три просто викликають `pubsub.PublishAsync(ChatId)`. Зареєстровано у `Program.cs` як singleton + 3 `bus.AddConsumer<>()`.
- **ChatView.razor:** новий `_chatChangedSubscription = ChatChangedPubSub.Subscribe(ChatId, OnChatChangedAsync)` + `OnChatChangedAsync` → `ReloadMessagesAsync()` → `InvokeAsync(StateHasChanged)`. Dispose у `DisposeAsync`. **+1 subscription у Razor проти +3 якщо робити окремі pubsub-и** — головна перевага уніфікованого pubsub.
- **Тести:** 0 нових. Patern + mappers + consumers — pass-throughs; кожен шар окремо вже tested через попередні Day 17/20/21 (MessageSentEvent → outbox → mapper → RabbitMQ → consumer → pubsub → ChatView). 115/115 passing.

**TODO:**
- Optimistic UI для send/retract — поки `Retract` чекає round-trip outbox+RabbitMQ (~2.5s). Локально показувати "[retracted]" одразу.
- `ReactionAdded` race: коли локальний юзер тицяє emoji у власне повідомлення, чекає round-trip щоб побачити свою ж reaction. Окрема ergonomics-fix.

## Step 29 (2026-05-31): Online presence push (гібрид push + safety-net polling) ✅
- **Тех-борг:** ChatView мав 3-секундний polling що ходив на `/presence/batch` для оновлення online-dots — останній regular polling у app. Заміна на push, з low-rate fallback для edge case browser-close.
- **Чому не тільки push:** explicit `GoOffline` POST публікує event миттєво, але якщо юзер просто закриває tab, ніхто не викликає `/presence/offline` — heartbeat припиняється і Redis TTL (30s) знімає presence без emission events. Тож **гібрид:** push для свідомих переходів (миттєвий UX), polling як safety net для browser-close (~30s найгірший випадок stale UI).
- **Contracts:** `UserCameOnlineIntegrationEvent` + `UserWentOfflineIntegrationEvent` (тільки `UserId` — broadcast на всіх Web instances хто підписаний).
- **Presence.Application:**
  - `HeartbeatCommandHandler` отримав `IPublishEndpoint`. **Publish тільки на transition offline→online** (під existing if-guard `if (presence.Status == OnlineStatus.Online) return;`); subsequent heartbeats тихі.
  - `GoOfflineCommandHandler` отримав `IPublishEndpoint`. Publish тільки коли реально transit Online→Offline.
  - **Direct publish (без outbox)** — як `UserTypingIntegrationEvent` (Day 17). Presence ефемерний; втрата події не критична — fallback polling + наступний heartbeat виправлять.
- **Web (нова папка `Services/Presence/`):** `IPresencePubSub.Subscribe(userId, Func<bool, Task>)` + impl + 2 consumers + DI reg. Per-user subscription (на відміну від per-chat для typing/messages) — presence per-юзер state. ChatView subscribe-ається до кожного active member чату.
- **ChatView.razor:**
  - `SubscribeToPresenceForMembers()` — для кожного member з `_chat.Members` (active, не сам юзер) робить `PresencePubSub.Subscribe(userId, OnPresenceChangedAsync)`, зберігає у `_presenceSubscriptions` list. Dispose всіх у `DisposeAsync`.
  - `OnPresenceChangedAsync(bool _)` → `RefreshPresenceAsync()` (re-fetch batch). UI зберігає тільки `_onlineCount` aggregate, тож на transition простіше переоб'явити цілий batch ніж тримати per-user dict.
  - **Poller** змінив поведінку: timer тіктає кожні 3s, але `RefreshPresenceAsync` викликає тільки кожен 10-тий тік (~30s) — лічильник `_pollTick`. Typing sweep лишається на 3s (5s TTL вимагає frequent ticks).
- **Тести:** +3 нових (`HeartbeatCommandHandlerTests`: publish-assertion на cold start; +1 doesn't-publish коли already online; новий `GoOfflineCommandHandlerTests` з 3 тестів). 118/118 passing.

**Що цей крок завершує:** усі real-time UI оновлення тепер push'ові. Залишилось **тільки одне місце з polling-ом** — typing sweeper у ChatView (3s, для TTL expiry без events) і 30-секундна presence safety net. Усе інше event-driven.

**TODO:**
- Background job у Presence: при Redis TTL expiry для presence-ключа emit `UserWentOfflineIntegrationEvent` (через keyspace notifications або periodic Redis SCAN + diff проти Mongo). Тоді можна повністю прибрати ChatView presence polling.
- `Notifications.razor` 3-сек page state polling — теж залишилось, окремий крок.

## Step 30 (2026-05-31): Chats/Messaging extraction — Phase 1 (scaffold + Domain) ✅
- **Архітектурні рішення** (узгоджено з юзером):
  - **Два окремих сервіси** `chats` + `messaging` замість одного об'єднаного. True DDD-розділ; Messaging пізніше будуватиме local membership read-model з Chats (як Presence Step 25).
  - **Cross-context `user.IsPremium`** для reaction limit → Web BFF пропихатиме `IsPremium` як параметр у `AddReactionCommand`. Уникаємо cross-service queries.
  - **Phase 1 scope:** тільки створити 8 нових проектів і перенести Domain layer — без зачіпання monolith. Application/Infrastructure/Api/BFF — окремі фази.
- **Створено:** 8 csproj:
  - `src/services/chats/TelegramLike.Chats.{Domain, Application, Infrastructure, Api}/`
  - `src/services/messaging/TelegramLike.Messaging.{Domain, Application, Infrastructure, Api}/`
  - ProjectReferences ланцюжком: Api → Infrastructure → Application → Domain.
- **Base types скопійовано** у кожен `Domain/Common/` (`AggregateRoot`, `Entity`, `IDomainEvent`) з namespaces `TelegramLike.Chats.Domain.Common` / `TelegramLike.Messaging.Domain.Common` — кожен Domain незалежний.
- **Chats.Domain отримав 19 файлів** (агрегати Chat/DirectChat/GroupChat/BroadcastChannel, Member entity, 10 events, IChatRepository, 4 VOs). Namespaces переписані з `TelegramLike.Domain.Chats` → `TelegramLike.Chats.Domain`.
- **Messaging.Domain отримав 14 файлів** (Message agg, Reaction entity, 4 events, IMessageRepository, 7 VOs). Namespaces — `TelegramLike.Messaging.Domain`.
- **Сross-deps confirmed clean:** Chats не посилається на Messaging і навпаки — кожен domain самодостатній.
- **Monolith лишився повністю недоторканим:** `src/TelegramLike.Domain/Chats|Messaging/` працює як раніше, всі razor pages і tests працюють як раніше. Це навмисний "additive-only" крок щоб нічого не зламати.
- **Тести:** 118/118 (нічого нового; новий код — copy без логіки).

**TODO (наступні Phase):**
- **Phase 2:** Application layer (Commands/Queries/Handlers + IntegrationEvent mappers) у обидва сервіси + Contracts ще не зачеплено (events лишаються у `TelegramLike.Contracts`).
- **Phase 3:** Infrastructure (Mongo repos для chats/chat_members/messages/message_read_receipts/hidden_messages + outbox + MassTransit).
- **Phase 4:** Api shells (JWT auth + HealthChecks + OpenTelemetry, як для Notifications/Presence).
- **Phase 5:** Web BFF clients (`IChatsApi`/`IMessagingApi`); переписати razor pages з `IMediator.Send` на HttpClient API.
- **Phase 6:** Видалити з monolith Chats+Messaging Domain/Application/Infrastructure; monolith лишається Identity-only + BFF.
- **Phase 7:** docker-compose: 2 нові сервіси + JWT propagation + healthcheck.
- **Phase 8 (opt):** Messaging local membership read-model з Chats integration events для відновлення strict `IsActiveMember` check у `SendMessage`.

## Step 31 (2026-05-31): Chats/Messaging extraction — Phase 2 (Application) ✅
- **Chats.Application:** усе перенесено (12 command handlers + validators + 3 query handlers + 3 mappers). Cross-context **`IUserRepository`** залежність у 3 create handlers (CreateGroupChat/CreateBroadcastChannel/CreateDirectChat) **прибрана** — Identity лишається у monolith, Chats trust JWT-authenticated caller. Створено local `IIntegrationEventMapper` interface і `IChatQueryService` interface у `Chats.Application.Common/`.
- **Messaging.Application:** усе перенесено (7 command handlers + validator + 2 query handlers + 4 mappers). Cross-context **`IChatRepository`** і **`IUserRepository`** залежності прибрані:
  - `SendMessageCommand` тепер приймає `Recipients: IReadOnlyList<Guid>` + `IsBroadcast: bool` як параметри — Web BFF тягне з `ChatsApi` перед викликом.
  - `AddReactionCommand` приймає `ActorIsPremium: bool` — Web BFF читає з session і пробросує (не IUserRepository call).
  - `RetractMessageCommand` приймає `ActorIsModerator: bool` — Web BFF робить role-check через `ChatsApi`.
  - `MarkMessageAsReadCommand` приймає `IsBroadcast: bool` — також з Web BFF.
  - `GetChatMessagesQuery` — прибрано membership check (Web BFF).
- Створено local interfaces у `Messaging.Application.Common/`: `IIntegrationEventMapper`, `IMessageReadReceiptRepository`, `IHiddenMessageRepository`, `IMessageQueryService`.
- **Important regression (документовано в коді):** Messaging тепер **fail-open для всіх членів**. Якщо хтось обходить Web BFF, може send/read/retract/react у будь-якому чаті. Phase 8 поверне strict-validation через local `IChatMembershipReadModel` (patern Step 25).
- **Monolith лишився повністю недоторканим.** Нові Application проекти — parallel copy, не використовуються нікиде поки.
- **Тести:** 118/118 (нічого не змінено у production коді).

**TODO для Phase 5 (Web BFF):**
- `IChatsApi.GetActiveRecipientsAsync(chatId, excludeUserId)` — для `SendMessageCommand.Recipients`.
- `IChatsApi.GetChatTypeAsync(chatId)` — для `IsBroadcast` flag (можна об'єднати з GetChatById endpoint).
- `IChatsApi.IsModeratorAsync(chatId, userId)` — для `ActorIsModerator`.
- Web BFF читає `IsPremium` з cookie/session перед `AddReactionCommand`.

## Step 32 (2026-05-31): Chats/Messaging extraction — Phase 3 (Infrastructure) ✅
- **Chats.Infrastructure**: скопійовано `ChatDocument` + `ChatMemberDocument` + `ChatRepository` (дві колекції з Mongo транзакцією) + `ChatQueryService`. Outbox під власним namespace (`OutboxPublisherOptions/Message/Document/IOutboxStore/MongoOutboxStore/IDomainEventDispatcher/OutboxDomainEventDispatcher/OutboxPublisherHostedService`) — повна автономність, ніяких залежностей від моноліту. `DependencyInjection.AddChatsInfrastructure` реєструє Mongo/Repos/Outbox/MassTransit з `vhost: telegramlike`.
- **Messaging.Infrastructure**: скопійовано `MessageDocument` (з `AttachmentDocument/ReactionDocument/ForwardReferenceDocument`) + `MessageRepository` + `MessageQueryService` + `HiddenMessageRepository` + `MessageReadReceiptRepository`. Окремий Outbox bundle (як у Chats). `DependencyInjection.AddMessagingInfrastructure` аналогічно реєструє все.
- **Колекції Mongo:** обидва сервіси використовують ту ж саму базу `telegramlike` поки що (single-DB, multiple-services підхід — без cross-service writes завдяки aggregate boundaries). Phase 7 розгляне per-service DB.
- **Outbox isolation:** кожен сервіс має власну колекцію `outbox` (поки в спільній БД) і власний publisher loop — fanout до власних integration events працює незалежно.
- **NuGet:** додано MassTransit 8.3, MassTransit.RabbitMQ 8.3, MediatR 14.1, MongoDB.Driver 3.8, Microsoft.Extensions.{Configuration/DI/Hosting/Logging/Options}.Abstractions 10.0.7.
- **Тести:** 118/118 (нічого не змінено у production коді — нові Infrastructure проекти ще ніким не використовуються; integration tests з'являться у Phase 4 разом з Api).

**TODO для Phase 4 (Api shells):**
- 2× Program.cs з: JWT auth (Bearer + JwtServiceAuth scheme як у Notifications/Presence), HealthChecks (Mongo + masstransit-bus), OpenTelemetry → Jaeger, MediatR registration.
- Minimal API endpoints за кожною Command/Query.
- Dockerfile + appsettings.json.

## Step 33 (2026-05-31): Chats/Messaging extraction — Phase 4 (Api shells) ✅
- **Chats.Api (port 8083):** Program.cs з JWT Bearer auth (issuer=telegramlike-web, audience=telegramlike-services, same secret як у Notifications/Presence), OpenTelemetry → Jaeger (`telegramlike.chats` service name), HealthChecks (Mongo + auto `masstransit-bus`), MediatR. 11 endpoints у групі `/chats` (всі `RequireAuthorization`):
  - GET `/chats/my`, `/chats/{id}`, `/chats/{id}/members`
  - POST `/chats/direct`, `/chats/group`, `/chats/broadcast` (повертають 201 + `ChatCreatedResponse`)
  - POST `/chats/{id}/join`, `/chats/{id}/leave`, `/chats/{id}/members/{userId}/kick`, `/chats/{id}/members/{userId}/role`, `/chats/{id}/transfer-ownership`
  - PATCH `/chats/{id}` (rename)
- **Messaging.Api (port 8084):** аналогічно. 8 endpoints:
  - POST `/messages/` (send), GET `/messages/{id}`
  - POST `/messages/{id}/reactions`, DELETE `/messages/{id}/reactions/{emoji}`, POST `/messages/{id}/retract`, `/read`, `/hide`
  - GET `/chats/{id}/messages` (paged)
- **JsonStringEnumConverter** включено в обидва сервіси — enum'и (ChatType/MemberRole/MemberStatus/AttachmentType/Emoji) серіалізуються як strings, щоб Web BFF Phase 5 міг мати свої власні enum types без посилань на Chats.Domain/Messaging.Domain.
- **`UserId` витягується з JWT** (`sub` claim → Guid). Body request DTOs (CreateGroupChatRequest тощо) НЕ містять userId — це йде з токена.
- **`SafeSend`/`SafeSendVoid` helpers** обгортають handler-throws: `InvalidOperationException`/`ArgumentException` → 400, `UnauthorizedAccessException` → 403. Patern збігається з тим, що використано у Notifications.Api.
- **Dockerfile** + **appsettings.json** (з окремими БД: `telegramlike_chats`, `telegramlike_messaging`) + **launchSettings.json** (8083/8084) — за зразком Notifications.Api.
- **NuGet:** додано до обох Api: AspNetCore.HealthChecks.MongoDb 9.0, MediatR 14.1, Microsoft.AspNetCore.Authentication.JwtBearer 9.0, OpenTelemetry.{Exporter.OpenTelemetryProtocol,Extensions.Hosting,Instrumentation.AspNetCore,Instrumentation.Http} 1.15.x.
- **Тести:** 118/118 (нічого не змінено у production коді — нові Api запускаються самостійно як shells, ще не підключені до docker-compose і Web BFF).

**TODO для Phase 5 (Web BFF):**
- Створити `IChatsApi` + `IMessagingApi` HttpClient-абстракції у `TelegramLike.Web/Services/`.
- Перенести razor pages з `IMediator.Send(...)` на `IChatsApi.CreateGroupChatAsync(...)` тощо.
- BFF робить `GetActiveRecipientsAsync` + `GetChatTypeAsync` + `IsModeratorAsync` через ChatsApi перед викликом MessagingApi (для відновлення Recipients/IsBroadcast/ActorIsModerator).
- `IsPremium` з cookie/session.
- JWT issuer = "telegramlike-web", token підписується тим же ServiceAuth:JwtSecret що використовується у Chats/Messaging Api.