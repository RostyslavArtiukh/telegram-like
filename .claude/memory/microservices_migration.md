---
name: microservices-migration
description: Інкрементальна міграція з modular monolith у мікросервіси — стратегія та прогрес
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

Стратегічне рішення (2026-05-24): міграція з Clean Architecture monolith у мікросервіси інкрементально, по 1 контексту за раз.

**Чому інкрементально:** Big bang переписування на 5 сервісів одночасно — antipattern. Виносимо по одному, починаючи з Notifications (найслабше зв'язаний контекст). Кожен крок reversible.

**Порядок винесення (плановано):**
1. **Notifications** — найслабша cohesion з рештою. День 12.
2. **Presence** — теж простий, ефемерний стан. Пізніше.
3. **Identity** — обережно, бо від нього залежить auth усього.
4. **Chats** + **Messaging** — найважче, дуже тісно зв'язані. Можливо лишити разом.

## Архітектурні рішення (узгоджені)
- **Cross-service queries:** embed recipients у integration events. НЕ робимо HTTP-виклики між сервісами. Event = self-contained.
- **Web → Service:** Blazor Web стає BFF, має `HttpClient`-clients (`INotificationsApi` тощо). API Gateway поки overkill.
- **Контракти:** окремий `TelegramLike.Contracts` проект (POCO records), посилаються обидва сервіси. У monorepo NuGet — зайвий.
- **Передача auth між Web BFF і сервісами:** TODO (День 13). Поки cookie-based auth у monolith.

## Прогрес

### День 11 (2026-05-24) ✅ — Notifications preparation
**Why:** перш ніж фізично виносити Notifications, треба прибрати залежність `FanoutChatNotificationCommandHandler` на `IChatRepository`. Без цього — сервіс не зможе працювати ізольовано.

**How to apply:** коли додаватимеш нові integration events для контекстів які плануються до виділення — слідуй паттерну: recipients обчислюються в публікуючому контексті і embed у domain event, потім у integration event.

**Конкретно зроблено:**
- Створено `src/TelegramLike.Contracts/TelegramLike.Contracts.csproj` (POCO records, без залежностей)
- Перенесено `IIntegrationEvent` + `MessageSentIntegrationEvent` + `MemberJoinedIntegrationEvent` + `MemberKickedIntegrationEvent` у Contracts
- У всіх трьох integration events додано `Recipients: IReadOnlyList<Guid>`
- У відповідних domain events (`MessageSentEvent`, `MemberJoinedEvent`, `MemberKickedEvent`) додано `Recipients`
- `Chat.RecipientsExcept(actorUserId)` — internal helper у базовому aggregate
- `Message.Send(...)` приймає `recipients` параметр
- `SendMessageCommandHandler` обчислює recipients з `chat.ActiveMembers.Where(m => m.UserId != author.UserId)` і передає у `Message.Send`
- `FanoutChatNotificationCommand` тепер містить `Recipients`, `FanoutChatNotificationCommandHandler` НЕ викликає `IChatRepository`
- Consumers пробросують `Recipients` з integration event у command
- Mappers (`Application/<Context>/IntegrationEvents/`) тільки passthrough — Application все ще посилається на Domain, бо мапить domain → integration

### День 12 (2026-05-24) ✅ — Notifications як окремий сервіс + Web BFF (об'єднано з Day 13)
**Why:** виносимо Notifications першим бо найслабша cohesion. Робимо Day 12+13 разом, бо інакше UI зламається на проміжному стані.

**How to apply:** для винесення інших контекстів (Presence наступний) — копіюй цей паттерн.

**Що зроблено:**

*Структура service:*
- `src/services/notifications/TelegramLike.Notifications.Domain/` — Notification + VOs + events + `INotificationRepository`. Власна копія base types (`Common/AggregateRoot.cs`, `Entity.cs`, `IDomainEvent.cs`) — service не залежить від monolith Domain.
- `src/services/notifications/TelegramLike.Notifications.Application/` — MarkAsRead/MarkAll/FanoutChatNotification commands, GetFeed/GetUnreadCount queries, `INotificationQueryService` interface, `NotificationDto`/`NotificationFeedDto`.
- `src/services/notifications/TelegramLike.Notifications.Infrastructure/` — `MongoNotificationRepository`, `MongoNotificationQueryService`, 3 MassTransit consumers (MessageSentConsumer/MemberJoinedConsumer/MemberKickedConsumer) — переїхали з monolith. `AddNotificationsInfrastructure(IConfiguration)` DI extension.
- `src/services/notifications/TelegramLike.Notifications.Api/` — ASP.NET Core minimal API.

*API endpoints (порт 8081):*
- `GET /notifications?before&pageSize&unreadOnly` → `NotificationFeedApiDto`
- `GET /notifications/unread-count` → `{ Count: long }`
- `POST /notifications/{id}/read` → 204
- `POST /notifications/read-all` → 204
- `GET /health` → `{ status: "ok" }`
- Auth: header `X-User-Id` (`Guid`); якщо нема або невалідний → `401 Unauthorized`. Внутрішня мережа = trust boundary.
- ContractMappers (`Api/Mapping/`) конвертують internal `NotificationDto` → public `NotificationApiDto` з Contracts.

*Власна Mongo БД:* `telegramlike_notifications` (окрема від monolith `telegramlike`)

*RabbitMQ:* спільний broker. Monolith тільки публікує (через outbox). Notifications підписується через MassTransit `bus.AddConsumer<>()`. Власні exchange/queue endpoints за іменами integration events.

*Web BFF:*
- `Web/Services/NotificationsApi/INotificationsApi.cs` — interface для UI
- `NotificationsApiClient` — typed HttpClient impl, повертає Contracts DTOs
- `UserIdHeaderHandler : DelegatingHandler` — пробросує `X-User-Id` з cookie auth (`CurrentUserAccessor`)
- DI: `AddHttpClient<INotificationsApi, NotificationsApiClient>().AddHttpMessageHandler<UserIdHeaderHandler>()` + `AddTransient<UserIdHeaderHandler>()`
- Config: `NotificationsApi:BaseUrl` (localhost:8081 локально, http://notifications:8080 у compose)

*Razor pages переписані:*
- `Notifications.razor`: `IMediator` → `INotificationsApi`, типи з Contracts (`NotificationApiDto`, `NotificationStatusContract`, `NotificationTypeContract`)
- `NavMenu.razor`: те ж саме для unread count

*Contracts розширено:* у `Contracts/Notifications/NotificationApiTypes.cs` — POCO records та enums для BFF.

*Docker compose:* новий сервіс `notifications` (порт 8081 на хост), залежить від mongodb + rabbitmq. `web` залежить від `notifications: service_started`.

**Гарантії:**
- Service працює повністю autonomous: власна БД, власна підписка на події, recipients вже у payload events (нема cross-context queries).
- Failure isolation: якщо Notifications.Api down, monolith UI не падає, але notification queries з UI повертають помилку.
- Tests: 6 проектів (48 + 22 + 15 monolith; 8 + 5 + 4 Notifications) = 102 passing.

**Що ще НЕ зроблено (TODO):**
- Auth: `X-User-Id` довіряємо як є. Для прод — JWT або mTLS.
- Distributed tracing (OpenTelemetry/Jaeger)
- Окремий RabbitMQ vhost (`/notifications`)
- Health probes у Notifications.Api для compose healthcheck
- Інші сервіси (Presence наступний по плану)

### День 15 (2026-05-30) ✅ — Presence як другий сервіс
**Why:** довести reusability паттерну Day 12+14 на другому сервісі. Presence — простий, без integration events на вході.

**How to apply:** для наступних сервісів — копіюй цей рецепт.

**Зроблено:**
- 4 проекти у `src/services/presence/`: Domain (UserPresence + OnlineStatus + events + IRepository + власна Common/), Application (commands/queries + `IPresenceCache`/`ITypingIndicatorService`/`IUserPresenceQueryService` в `Abstractions/`+`Queries/`), Infrastructure (`MongoUserPresenceRepository` + `MongoUserPresenceQueryService` + `RedisPresenceCache` + `RedisTypingIndicatorService` + DI), Api (minimal API + AddJwtBearer config copy-paste з Notifications).
- Endpoints на :8082: `POST /heartbeat`, `POST /offline`, `GET /{userId}`, `POST /typing/{chatId}/start|stop`, `GET /typing/{chatId}`, `GET /health` (public).
- Власна Mongo `telegramlike_presence` (колекція `user_presence`). Спільний Redis з monolith (ключі вже namespaced `presence:`/`typing:`).
- **JWT auth повністю reused** — той же `ServiceAuthHandler` (Web) тепер обслуговує і Notifications, і Presence. `UserIdKey` перенесено з `NotificationsApiClient` на `ServiceAuthHandler` (shared option key).
- Web BFF: `IPresenceApi` + `PresenceApiClient` за паттерном Notifications. Реєстрація `AddHttpClient<IPresenceApi, PresenceApiClient>().AddHttpMessageHandler<ServiceAuthHandler>()`.
- `MainLayout.razor` шле heartbeat кожні 20 сек через `IPresenceApi`, GoOffline на dispose.
- **Cross-context dropped:** `StartTypingCommandHandler` більше НЕ викликає `IChatRepository.GetByIdAsync` — Presence-сервіс не має доступу до Chats БД. Зараз trust JWT-authenticated caller. Для відновлення — підписатись на `MemberJoined/Left/Kicked` integration events і будувати local read model.
- Видалено з monolith: всі Presence файли (Domain/Application/Infrastructure) + 3 інтерфейси у Application/Common/Interfaces/.
- Тести: новий `TelegramLike.Presence.{Domain,Application,Infrastructure}.Tests` (18 тестів). Окрема `RedisFixture` у Infrastructure.Tests (тільки Redis, без Mongo).
- 102 тести зелені (9 test projects).

**Що цей день довів:** паттерн з Day 12+14 reusable. Виносити сервіс ≈ година роботи (5 csproj move + Api shell з copy-paste JWT setup + BFF client за template + 1 razor edit + tests move).

### Наступне (кандидати для винесення)
- **Messaging** + **Chats** разом — найскладніше, бо тісно зв'язані (SendMessage перевіряє ActiveMember у chat). Або:
  - Залишити їх разом як "core messaging service"
  - Розбити пізніше через event-driven local read models
- **Identity** — обережно, бо auth провайдер. Якщо виносити, потрібен IdP-сервіс (OAuth2/OIDC) і Web стає client.
- Або **не виносити більше** — три сервіси (Notifications, Presence + monolith з Chats/Messaging/Identity) це вже валідна архітектура. Кожне розбиття додає operational complexity.

## Що **не** робимо у міграції (поки)
- Service discovery (Consul/eureka) — DNS у docker-compose досить
- Distributed tracing (OpenTelemetry, Jaeger) — окремий день
- Окремий CI/CD per service — monorepo, спільний build
- gRPC між сервісами — RabbitMQ events + HTTP досить
- Розділяти інші контексти — спочатку доведемо що з Notifications працює
