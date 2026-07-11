---
name: shared-projects-naming
description: "Рефакторинг 2026-07-11 — shared-проєкти по шарах, людський неймінг (мапа старе→нове), правило видалення одноразових інтерфейсів"
metadata: 
  node_type: memory
  type: project
  originSessionId: 0d99dc3b-dd83-4014-8659-07a5e5762cc9
---

Рефакторинг (2026-07-11, сесія після [TL-93]): три задачі — дедуплікація у shared-проєкти, людський неймінг, видалення одноразових інтерфейсів. Build чистий, всі 343 тести зелені, MAUI (App.slnx) білдиться.

**Shared-проєкти (`src/shared/`), по шарах — щоб Domain не тягнув Mongo/ASP.NET:**
- `TelegramLike.Domain.ServiceDefaults` (0 залежностей): `ObjectWithId`, `ObjectWithEvents`, `IChangeEvent`, `DomainException`+`ForbiddenException`. Усі 5 Domain-проєктів мають ProjectReference + глобальний `<Using>`.
- `TelegramLike.Application.ServiceDefaults` (MediatR, FluentValidation, Contracts): `ValidateRequestBeforeHandling` (екс-ValidationBehavior), `IIntegrationEventMapper` (член `ChangeEventType`).
- `TelegramLike.Infrastructure.ServiceDefaults` (Mongo, MassTransit, Redis): `OutgoingEvents/` (екс-Outbox: `OutgoingEvent(Document)`, `OutgoingEventsStore` (інтерфейс IOutboxStore видалено), `IOutgoingEventsWriter`/`OutgoingEventsWriter` (екс-IDomainEventDispatcher; інтерфейс лишився — тест-даблер `NoOpOutgoingEventsWriter`), `OutgoingEventsSender(Options)`, `AddOutgoingEvents`) + `MongoDbSetup.AddMongoDb`, `RedisSetup.AddRedis`, `RabbitMqSetup.AddRabbitMqBus(cfg, bus => bus.AddConsumer<...>())`.
- `TelegramLike.Api.ServiceDefaults` — як був (JWT + ApiControllerBase). `DomainExceptionFilter` НЕ шарився: у кожного сервісу свідомо різний маппінг (presence — no-op).

**Мапа неймінгу (старе → нове):**
- `Entity` → `ObjectWithId`; `AggregateRoot` → `ObjectWithEvents`; `IDomainEvent` → `IChangeEvent`
- `RaiseDomainEvent` → `RecordEvent`; `DomainEvents` → `PendingEvents`; `ClearDomainEvents` → `ClearPendingEvents`
- `Reconstitute` → `FromStorage`; папка/namespace `Infrastructure/Persistence` → `Infrastructure/Storage`
- клас `DependencyInjection` → `InfrastructureSetup` (методи `Add<X>Infrastructure` без змін)
- Actor/Target — під кожну команду: Kick(`MemberToKickUserId`, `KickedByUserId`), ChangeMemberRole(`MemberToChangeUserId`, `ChangedByUserId`), RenameChat(`RenamedByUserId`), RetractMessage(`RetractedByUserId`, `RetractedByModerator` — wire-поле `retractedByModerator`!), AddReaction(`UserIsPremium` — wire `userIsPremium`), Notifications `ActorId` → `TriggeredByUserId`
- Mongo-колекція `outbox` → `outgoing_events`; конфіг-ключі `Outbox:*` → `OutgoingEvents:*` (ніде в compose/k8s не задавались)
- Application/`Common` розібрано: identity → `Security/`, messaging → `Storage/` (+`ConcurrencyRetry` в корінь), chats `IChatQueryService` → `Queries/`, presence `Abstractions/` → `Storage/`
- «idempotency key» у коментарях → «duplicate-protection key»; HTTP-заголовок `Idempotency-Key` НЕ чіпали (стандарт, на нього зав'язаний retry у SDK)

**Правило видалення інтерфейсів (задача «одна реалізація — інтерфейс не потрібен»):**
видаляли тільки якщо (а) рівно одна продакшн-реалізація, (б) ніде не мокається у тестах, (в) інтерфейс і реалізація в одному проєкті (щоб не інвертувати шари Application→Infrastructure). Видалено: Web `I*PubSub` ×5, realtime `IChatMembershipTracker`, SDK `IChatsApi`/`IMessagingApi`/`IPresenceApi`/`INotificationsApi`/`IIdentityUsersApi`/`ITelegramLikeRealtimeClient` (клієнти стали public, DI реєструє конкретні типи, `AddHttpClient<TClient>`), infra `IOutboxStore`. Залишено: усі Application/Domain-інтерфейси з impl в Infrastructure (шари), усе що мокається (`IIdentityAuthApi`, репозиторії тощо), `ISessionStore`/`IAccessTokenProvider` (по 2 реалізації).

**Why:** явний запит юзера ([[readable-naming-and-mudblazor]]) — людські імена без DDD/patterns-жаргону; дублікати базових класів у 5 сервісах намножились під час міграції з моноліту.
**How to apply:** нові сервіси беруть базові типи/сетапи з shared-проєктів, нічого не копіюють; нові інтерфейси створювати лише коли є друга реалізація або потреба мокати; імена команд/полів — під дію («KickedByUserId», не «ActorUserId»).
