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

**Покриття:** 56 Domain + 21 Application + 14 Infrastructure = 91 тестів. Не претендуємо на 100% — покрите ядро DDD-інваріантів і ключові cross-context handlers.
