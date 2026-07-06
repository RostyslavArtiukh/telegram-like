---
name: testing-setup
description: Стек і ключові рішення тестування TelegramLike (xUnit + FluentAssertions + NSubstitute + Testcontainers)
metadata: 
  node_type: memory
  type: project
  originSessionId: 62cc2e6a-d21e-47f2-9647-8294fc3dff38
---

Тестовий пакет TelegramLike: xUnit + FluentAssertions для всіх трьох test-проектів; NSubstitute у Application.Tests для мокування репозиторіїв та `ISender`; Testcontainers.MongoDb 3.10 + Testcontainers.Redis 3.10 у Infrastructure.Tests.

**Why:** День 8 — інтеграційні тести зі справжніми Mongo replica set (бо `ChatRepository` використовує `WithTransactionAsync`) та Redis (бо presence/typing — там).

**How to apply:**
- Internal класи у `TelegramLike.Infrastructure` (наприклад `ChatRepository`, `NotificationRepository`, `RedisPresenceCache`) доступні з тестового проекту через `<InternalsVisibleTo Include="TelegramLike.Infrastructure.Tests" />` в Infrastructure.csproj. Якщо створиш новий internal — automatically видимий.
- `IntegrationContainersFixture` (xUnit `IAsyncLifetime`) піднімає Mongo (`MongoDbBuilder().WithReplicaSet()`) + Redis один раз; всі інтеграційні тести шарять через `[Collection(IntegrationCollection.Name)]`.
- **Критично:** після `new MongoClient(_mongo.GetConnectionString())` driver виявляє топологію ReplicaSet і слідує за advertised host (`localhost:27017` зсередини контейнера) — підключення впаде з socket refused. Виправлення у fixture: `settings.DirectConnection = true; settings.ReplicaSetName = null;`. Транзакції на single-node все ще працюють бо сервер сам primary.
- Перший прогін після `docker compose down` чи новий машині = pull mongo:7 + redis:7-alpine, може зайняти 3–5 хв. Подальші — секунди.
- Application.Tests мокають `Chat` напряму через справжні `GroupChat.Create` / `BroadcastChannel.Create` (агрегати legkі), а репозиторії — `Substitute.For<...>()`. Це краще ніж мокати поведінку чату.
- Не запускай Infrastructure.Tests без Docker — впадуть з timeout 30s на кожен тест. У CI треба docker-in-docker або pre-warm.
- **Транзакційні репозиторії потребують реплісету:** якщо Infrastructure-клас використовує `session.WithTransactionAsync` (multi-doc транзакція, напр. `MessageRepository`/`ChatRepository`), fixture МУСИТЬ викликати `new MongoDbBuilder("mongo:7").WithReplicaSet().Build()` — без цього Mongo кидає `Standalone servers do not support transactions`. Fixtures, що торкаються лише single-document колекцій (Presence/Notifications), обходяться без `.WithReplicaSet()`.
- Новий Infrastructure.Tests проєкт для сервісу з internal repository-класами: додай `<InternalsVisibleTo Include="TelegramLike.<Service>.Infrastructure.Tests" />` в `*.Infrastructure.csproj` (той самий патерн, що вже є в Identity/Notifications/Presence/Messaging/Gateway) — це лише test-scaffolding, не зміна поведінки.
- MassTransit consumer-и (`internal sealed class XConsumer : IConsumer<T>`) тестуються без брокера: `Substitute.For<ConsumeContext<T>>()` + `.Message.Returns(evt)` + `.CancellationToken.Returns(CancellationToken.None)`, викликаючи `consumer.Consume(ctx)` напряму. Для read-model idempotency краще прогнати консюмер двічі проти справжнього (Testcontainers) чи in-memory read-model, а не мокати сам read-model — це перевіряє і wiring, і фактичну ідемпотентність останнього запису.

**Покриття (після [TL-72] тестового аудиту):** 18 test-проєктів, 343 тести, усі зелені. Нові проєкти: `Messaging.Domain.Tests` (28, reaction limits/retract guards/broadcast count), `Messaging.Application.Tests` (32, SendMessage read-model membership+recipient-derivation, Get* read-IDOR, Retract server-side moderator, MarkAsRead broadcast count, AddReaction/RemoveReaction documented fail-open), `Messaging.Infrastructure.Tests` (29, MessageRepository optimistic-concurrency + idempotent duplicate insert, MessageReadReceiptRepository unique-index idempotency, MongoChatMembershipReadModel last-writer-wins + Role/IsModerator + legacy-doc read, membership-consumer redelivery idempotency), `Chats.Domain.Tests` (43, role hierarchy on GroupChat/BroadcastChannel, DirectChat rejects rename/kick/leave, TransferOwnership), `Chats.Application.Tests` (15, GetChatById/GetChatMembers read-IDOR regression, Join/Kick/ChangeMemberRole authorization), `Identity.Application.Tests` (16, AccountStatus login/session gate, idempotent register retry), `Gateway.Tests` (8, `RedactAccessTokenProcessor` token scrubbing). `Realtime.Api.Tests` extended +13 (`ChatMembershipTracker` + its 3 membership consumers). Не претендуємо на 100% — покрите ядро DDD-інваріантів і ключові cross-context/security-regression handlers; Chats Api-рівень (controllers) і деякі fail-open read paths (AddReaction/RemoveReaction/MarkAsRead non-member) свідомо лишені fail-open і задокументовані тестом, а не зафіксовані як баг.
