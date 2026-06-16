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
- **Cross-context dropped:** `StartTypingCommandHandler` більше НЕ викликає `IChatRepository.GetByIdAsync` — Presence-сервіс не має доступу до Chats БД. Зараз trust JWT-authenticated caller. **Step 25 (2026-05-31): відновлено** через подписку на `MemberJoined/Kicked/Left` integration events → local read-model `chat_memberships` у `telegramlike_presence`. Fail-open поки нема backfill існуючих чатів.
- Видалено з monolith: всі Presence файли (Domain/Application/Infrastructure) + 3 інтерфейси у Application/Common/Interfaces/.
- Тести: новий `TelegramLike.Presence.{Domain,Application,Infrastructure}.Tests` (18 тестів). Окрема `RedisFixture` у Infrastructure.Tests (тільки Redis, без Mongo).
- 102 тести зелені (9 test projects).

**Що цей день довів:** паттерн з Day 12+14 reusable. Виносити сервіс ≈ година роботи (5 csproj move + Api shell з copy-paste JWT setup + BFF client за template + 1 razor edit + tests move).

### Step 25 (2026-05-31) ✅ — Local membership read-model у Presence
**Why:** після Day 15 Presence trust-ав JWT caller для membership check (бо нема доступу до Chats БД). Реальна перевірка повернута через event-driven локальну read-модель — patern який буде reused для майбутніх extractions.

**How to apply:** коли наступний сервіс потребує знати стан з іншого контексту — НЕ роби HTTP-виклик у Chats; підпишись на відповідні integration events і будуй local materialized view. Composite Mongo Id (`"{x:N}:{y:N}"`) дає природній dedup + idempotent upsert без окремого unique index.

**Конкретно зроблено:**
- Новий `MemberLeftIntegrationEvent` у Contracts + mapper у monolith Application (Joined/Kicked вже існували з Day 11).
- Presence.Application: `IChatMembershipReadModel` (IsActiveMemberAsync / UpsertActiveAsync / RemoveAsync).
- Presence.Infrastructure: `MongoChatMembershipReadModel` (колекція `chat_memberships` з composite Id) + 3 тонких consumer'и (`MemberJoinedConsumer` → upsert, `MemberKickedConsumer`/`MemberLeftConsumer` → remove). `AddIntegrationMessaging` тепер реєструє consumers + `ConfigureEndpoints` (раніше тільки publish-only).
- `StartTypingCommandHandler` отримав `IChatMembershipReadModel` залежність + `ILogger`. **Fail-open**: якщо read-model не знає про пару → лог warning і пропускає. Tighten до fail-closed чекає backfill.
- Tests: 6 integration (Mongo Testcontainers) + 2 unit для StartTyping. 115 passing.
- Side fix: `Testcontainers.Redis` bump 3.10.0 → 4.12.0 щоб бути сумісним з новим `Testcontainers.MongoDb` 4.12.0 у тому ж проекті.

### Steps 30–36 (2026-05-31) ✅ — Chats + Messaging extraction (8-phase)
**Why:** доказати що паттерн Notifications+Presence масштабується на найскладнішу пару тісно-зв'язаних контекстів. Розбито на 2 сервіси `chats` (port 8083) + `messaging` (port 8084) — пов'язану функціональність легше evolve окремо, ніж склеювати назад.

**How to apply:** для майбутніх extractions використовуй цю 6-phase recipe (Phase 7 docker-compose ще TODO):
1. **Phase 1 (Step 30):** scaffold + Domain — `dotnet new classlib/web` всі 4 csproj per service, скопіювати Domain з namespace rewrite (PowerShell `Get-ChildItem | ForEach-Object` + `[System.IO.File]::WriteAllText` з namespace replacement), власна копія base types (`Common/AggregateRoot.cs`/`Entity.cs`/`IDomainEvent.cs`).
2. **Phase 2 (Step 31):** Application — скопіювати handlers + validators + queries + mappers, **дропнути cross-context dependencies** (IChatRepository з Messaging, IUserRepository з Chats) — замість них додати command parameters (`Recipients: IReadOnlyList<Guid>`, `IsBroadcast: bool`, `ActorIsPremium: bool`, `ActorIsModerator: bool`) які Web BFF enrich'ить. **Acceptable regression:** fail-open до часу як Phase 8 додасть local read-models.
3. **Phase 3 (Step 32):** Infrastructure — Mongo repos + autonomous Outbox bundle per service (повна копія, не shared) + MassTransit DI з vhost.
4. **Phase 4 (Step 33):** Api shells — Program.cs з JWT Bearer auth (same secret для всіх сервісів), HealthChecks (Mongo + auto `masstransit-bus`), OpenTelemetry → Jaeger, MediatR, Minimal API endpoints з groupRoute, **JsonStringEnumConverter** для stable enum payloads. Dockerfile + appsettings + launchSettings.
5. **Phase 5a (Step 34):** Web HttpClient clients — `IFooApi` + `FooApiClient` через існуючий `ServiceAuthHandler`. **Web-local contract enums + DTOs** з `[JsonStringEnumConverter]` — Web більше не залежить від Service.Domain.
6. **Phase 5b (Step 35):** Razor pages — `IMediator.Send` → API clients. **BFF-side enrichment робиться локально** з вже-завантаженого `ChatDetailsContract` (recipients = active members - author) — без додаткового HTTP round-trip.
7. **Phase 6 (Step 36):** Cleanup monolith — видалити `Domain/{Chats,Messaging}/`, `Application/{Chats,Messaging}/`, `Infrastructure/Persistence/MongoDB/Repositories/{Chat*,Message*,HiddenMessage*}`, trim DI (зняти IRepository/IQueryService реєстрації + IIntegrationEventMapper singletons; Outbox stack лишається dormant для майбутніх Identity events). Видалити obsolete test проекти повністю через `dotnet sln remove` + `rm -rf`.

**Phase 7 TODO:** docker-compose з обома новими сервісами + healthchecks + `web` depends_on chats/messaging healthy + env vars (`ChatsApi__BaseUrl=http://chats:8080`).
**Phase 8 TODO (opt):** Messaging local membership read-model з Chats integration events для відновлення strict `IsActiveMember` check у `SendMessage` (як Presence у Step 25).

**Що цей етап довів:**
- 6 phases × кілька годин = чистий extraction двох найскладніших контекстів. Кожна phase окремий commit, кожна reversible.
- **Web BFF тепер pure BFF** — тільки Identity handlers через IMediator + усі інші domain calls через HTTP clients.
- 57/57 тестів (зменшилось з 118, тільки за рахунок видалення dead монолітних тестів — нової логіки не сламано).
- **Monolith ≈ Identity + BFF + Infrastructure shell** (1 IUserRepository + dormant Outbox). Identity можна виносити наступним.

### Поточний стан архітектури (після Step 36)
- **Monolith (Web BFF):** Identity Domain/Application/Infrastructure + Web (Blazor Server) + HttpClient'и до 4 downstream services.
- **4 downstream services:** Notifications (8081), Presence (8082), Chats (8083), Messaging (8084).
- **Shared infra:** Mongo (per-service DB), Redis (presence/sessions), RabbitMQ (vhost `telegramlike`), Jaeger (OTel collector).
- **Що ще НЕ виносили:** Identity (потрібен IdP-сервіс OAuth2/OIDC для повної екстракції; зараз ОК як частина BFF).

### Identity extraction (Steps 39–42 done; [TL-43]+ remaining) (2026-06-07) 🚧 — останній контекст → standalone IdP. PAUSED після Phase 4. (Нейминг комітів з [TL-43] — див. [[nomenclature-step-not-day]].)
**Архітектурні рішення (узгоджено з юзером):**
- **Identity стає IdP** (не просто user-сервіс): випуск JWT переїжджає з Web → Identity. Усі 4 наявні сервіси треба переконфігурувати `ValidIssuer` з `telegramlike-web` → `telegramlike-identity` (Phase 5). Web більше не issuer — він exchange'ить session token на access-token у Identity і форвардить.
- **Browser login лишає Redis session-token handoff** (як зараз): `/login` → session token → `/auth/signin` обмінює на cookie. `RedisSessionService` переїхав у Identity.
- Identity — **останній контекст**, тож Phase 6 **розчиняє моноліт**: видаляємо `TelegramLike.Domain/Application/Infrastructure`, Web стає pure BFF (тільки Contracts + 5 HttpClient'ів). MassTransit-шину Web (pubsub consumers) переносимо з `Infrastructure.AddIntegrationMessaging` у Web-локальний extension.

**Зроблено й запушено (origin/master):** новий сервіс `src/services/identity/` (port 8085, БД `telegramlike_identity`):
- Phase 1 (Step 39): scaffold 4 csproj + Domain (User/VOs/events/IUserRepository, namespace `TelegramLike.Identity.Domain`).
- Phase 2 (Step 40): Application — RegisterUser/LoginUser + validators, GetUserById/GetUsernamesByIds/GetUserIdByUsername; `IPasswordHasher`/`ISessionService` переїхали; **новий `IAccessTokenIssuer`** + `ExchangeSessionQuery` (session→access JWT+claims, тонкі Api endpoints).
- Phase 3 (Step 41): Infrastructure — UserRepository/UserDocument, BcryptPasswordHasher, RedisSessionService, **`AccessTokenIssuer`** (HMAC, `iss=telegramlike-identity`). `AddIdentityInfrastructure`. БЕЗ RabbitMQ/outbox (Identity не має integration events).
- Phase 4 (Step 42): Api shell 8085 — public `/auth/register`, `/auth/login`, `/auth/token`; authed `/users/{id}`, `/users/by-ids`, `/users/by-username` (валідує власні токени). MediatR+ValidationBehavior+FluentValidation, Mongo+Redis health, OTel. **Smoke-перевірено локально** (register→login→token→authed, 401 без токена, 400 дубль). Той самий shared JWT secret що в усіх сервісах.

**Phase 5 ([TL-43]) — НЕ зроблено, готовий дизайн (наступна сесія):**
- ⚠️ **Scope-пастка:** `ServiceAuthHandler` (DelegatingHandler) пулиться IHttpClientFactory ОКРЕМО від Blazor circuit-scope. Інжектити scoped auth-state (CurrentUserAccessor) у handler НЕ МОЖНА — токен одного юзера прилетить іншому. Тому план «handler сам читає session_token» **хибний**.
- **Коректно:** scoped `ServiceTokenProvider` (інжектиться в КЛІЄНТИ, не в handler) резолвить access-token у circuit-scope: `CurrentUserAccessor.GetSessionTokenAsync()` → `IIdentityAuthApi.ExchangeAsync` → cache (IMemoryCache, TTL < token lifetime). Кладе токен у `request.Options`; handler лише чіпляє `Bearer` (як зараз робиться з `UserIdKey`/userId).
- **Файли Phase 5:** `CurrentUserAccessor` (+GetSessionTokenAsync читає `session_token` claim — він вже сетиться у [Web/Program.cs](src/TelegramLike.Web/Program.cs) /auth/signin); новий `Web/Services/IdentityApi/` — `IIdentityAuthApi` (register/login/exchange, **plain client без handler**) + `IIdentityUsersApi` (user-queries, **з handler**); `ServiceTokenProvider`; переробити `ServiceAuthHandler` (читати `AccessTokenKey` замість мінтити, прибрати `ServiceTokenIssuer`); 4 клієнти (Presence/Chats/Messaging — 1 chokepoint-helper кожен, Notifications — 5 inline) сетять access-token замість `UserIdKey`; `Program.cs` DI (AddMemoryCache, 2 identity clients, прибрати ServiceTokenIssuer); `/auth/signin` → `ExchangeAsync`; razor Login/Register → `IIdentityAuthApi`, ChatView(premium)/Home(direct-chat) → `IIdentityUsersApi`; **4 сервіси appsettings `ServiceAuth:Issuer`→`telegramlike-identity`** (атомарно з Web, інакше downstream 401).
- userId-параметри в 4 клієнтах лишаються (vestigial для auth, але йдуть у URL/body де треба) — не чіпати сигнатури/razor-call-sites.
- План-файл: `C:\Users\Ros\.claude\plans\optimized-squishing-quail.md`.

## Що **не** робимо у міграції (поки)
- Service discovery (Consul/eureka) — DNS у docker-compose досить
- Distributed tracing (OpenTelemetry, Jaeger) — окремий день
- Окремий CI/CD per service — monorepo, спільний build
- gRPC між сервісами — RabbitMQ events + HTTP досить
- Розділяти інші контексти — спочатку доведемо що з Notifications працює
