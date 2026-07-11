---
name: testing-setup
description: Стек і ключові рішення тестування TelegramLike (xUnit + FluentAssertions + NSubstitute + Testcontainers)
metadata: 
  node_type: memory
  type: project
  originSessionId: 62cc2e6a-d21e-47f2-9647-8294fc3dff38
---

Тестовий пакет TelegramLike: xUnit + FluentAssertions скрізь; NSubstitute для мокування репозиторіїв та `ISender`; Testcontainers.MongoDb + Testcontainers.Redis для інтеграційних тестів. **Структура [TL-95]: один test-проєкт на сервіс/компонент (8 всього)** — `TelegramLike.{Chats,Identity,Messaging,Notifications,Presence}.Tests` (усередині папки/неймспейси `.Tests.{Domain,Application,Api,Infrastructure}`) + `Client.Tests`, `Gateway.Tests`, `Realtime.Tests`. Csproj референсить лише верхній шар сервісу (Api або Infrastructure) — решта транзитивно. Тільки швидкі юніти без Docker: `dotnet test --filter "FullyQualifiedName!~.Tests.Infrastructure"`.

**Why:** День 8 — інтеграційні тести зі справжніми Mongo replica set (бо `ChatRepository` використовує `WithTransactionAsync`) та Redis (бо presence/typing — там).

**How to apply:**
- Internal класи Infrastructure доступні тестам через `<InternalsVisibleTo Include="TelegramLike.<Service>.Tests" />` в `*.Infrastructure.csproj` (після [TL-95] це ім'я всього per-service тест-проєкту). Якщо створиш новий internal — automatically видимий.
- `IntegrationContainersFixture` (xUnit `IAsyncLifetime`) піднімає Mongo (`MongoDbBuilder().WithReplicaSet()`) + Redis один раз; всі інтеграційні тести шарять через `[Collection(IntegrationCollection.Name)]`.
- **Критично:** після `new MongoClient(_mongo.GetConnectionString())` driver виявляє топологію ReplicaSet і слідує за advertised host (`localhost:27017` зсередини контейнера) — підключення впаде з socket refused. Виправлення у fixture: `settings.DirectConnection = true; settings.ReplicaSetName = null;`. Транзакції на single-node все ще працюють бо сервер сам primary.
- Перший прогін після `docker compose down` чи новий машині = pull mongo:7 + redis:7-alpine, може зайняти 3–5 хв. Подальші — секунди.
- Application.Tests мокають `Chat` напряму через справжні `GroupChat.Create` / `BroadcastChannel.Create` (агрегати legkі), а репозиторії — `Substitute.For<...>()`. Це краще ніж мокати поведінку чату.
- Не запускай Infrastructure.Tests без Docker — впадуть з timeout 30s на кожен тест. У CI треба docker-in-docker або pre-warm.
- **Транзакційні репозиторії потребують реплісету:** якщо Infrastructure-клас використовує `session.WithTransactionAsync` (multi-doc транзакція, напр. `MessageRepository`/`ChatRepository`), fixture МУСИТЬ викликати `new MongoDbBuilder("mongo:7").WithReplicaSet().Build()` — без цього Mongo кидає `Standalone servers do not support transactions`. Fixtures, що торкаються лише single-document колекцій (Presence/Notifications), обходяться без `.WithReplicaSet()`.
- **Не додавай прямий `PackageReference` на MassTransit у тест-csproj** — бери транзитивну версію сервіса (8.3.0). Прямий MassTransit 9.1.1 (був у старих Infrastructure.Tests) створював version skew: ламався `WebApplicationFactory` старт Api з RabbitMQ-топологією (`RabbitMqPublishTopology.CreateMessageTopology` кидав під час CreateBus). Це ж правило для інших пакетів, що вже приходять від сервіса.
- **Неймінг тестів — один стиль скрізь:** речення в snake_case, ідентифікатори коду зберігають свій регістр (`GetChatById_when_not_found_returns_404`, `Retract_binds_RetractedByModerator`, `DomainException_returns_400_with_ProblemDetails`). PascalCase-стиль (`UnknownPath_Returns404`) уніфіковано в [TL-95].
- MassTransit consumer-и (`internal sealed class XConsumer : IConsumer<T>`) тестуються без брокера: `Substitute.For<ConsumeContext<T>>()` + `.Message.Returns(evt)` + `.CancellationToken.Returns(CancellationToken.None)`, викликаючи `consumer.Consume(ctx)` напряму. Для read-model idempotency краще прогнати консюмер двічі проти справжнього (Testcontainers) чи in-memory read-model, а не мокати сам read-model — це перевіряє і wiring, і фактичну ідемпотентність останнього запису.

**Покриття (після [TL-72] аудиту + [TL-95] злиття проєктів):** 8 test-проєктів, 343 тести, усі зелені. Розкладка: Chats 100 (Domain 43 / Application 15 / Api 42), Messaging 130 (Domain 28 / Application 32 / Api 41 / Infrastructure 29), Identity 23, Notifications 21, Presence 32, Realtime 22, Gateway 8, Client 7. Не претендуємо на 100% — покрите ядро DDD-інваріантів і ключові cross-context/security-regression handlers; fail-open read paths (AddReaction/RemoveReaction/MarkAsRead non-member) свідомо задокументовані тестом. Відомі дірки: Chats.Infrastructure (ChatRepository: 2 колекції + Mongo транзакція — НЕ має інтеграційних тестів), Identity Api-рівень і session store, Notifications Mark*/feed handlers, Presence Stop_typing/GetPresence queries, Gateway route-generation. Деталі — у сесії [TL-95].
