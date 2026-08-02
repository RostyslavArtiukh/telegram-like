---
name: maintainability-review-fixes
description: "Аудит підтримуваності/масштабованості (2026-08-02) — 4 фікси [TL-117..120] (wire-імена подій, recipients лише в Messaging, декларативні Mongo-індекси, shared-шар як NuGet) + список масштабованості, який ще НЕ робили"
metadata: 
  node_type: memory
  type: project
  originSessionId: 1b8a1277-0be1-4ca8-b590-8e15e81a33e4
  modified: 2026-08-02T07:41:03.434Z
---

Огляд рішення на підтримуваність і масштабованість (2026-08-02). Знайшов 4 ризики підтримуваності + 8 стель масштабованості. **Зроблено лише 4 ризики підтримуваності, [TL-117..120], по коміту на пункт.** Список масштабованості лишився на окремий раунд.

**[TL-117] Wire-імена integration events.** Outbox зберігав CLR-ім'я типу й резолвив через `Type.GetType` через години. Перейменування/переміщення record-а в Contracts ламало ВСІ неопубліковані рядки, і rollback не рятував (рядки лишаються зі старим ім'ям). Тепер `[IntegrationEventName("context.event.v1")]` + реєстр `IntegrationEventNames` у Shared.Application; є fallback на CLR-резолв для legacy-рядків (тому деплой не потребує дренажу черги). ⚠️ Це чинить **лише збережені рядки** — MassTransit усе одно роутить за CLR type urn, тож перейменування досі ламає повідомлення в польоті й консюмерів. Деталі — [[integration-events-rabbitmq]].

**[TL-118] Recipients виводить лише Messaging.** Обидва Razor-хости (Web BFF + MAUI) рахували «всі активні мінус автор» і слали як `recipients`; Messaging той самий список уже виводив зі свого read-model і брав клієнтський лише як fallback. Тобто логіка жила в трьох місцях і давала вектор спуфінгу, коли fallback спрацьовував. Поле прибрано з дроту, SDK, команди й DTO; `ChatsApiClient.GetActiveRecipientsAsync` видалено. **Свідомо прийнята ціна (вибір юзера):** якщо чат ще не матеріалізований у read-model (~2 с після створення), у повідомлення порожня аудиторія → воно збережеться й читатиметься, але без нотифікації та realtime-push. `isBroadcast` навмисно ЛИШИВ клієнтський fallback: помилка тут запікається в збережене повідомлення назавжди, а пропущений fan-out — тимчасовий.

**[TL-119] Mongo-індекси декларуються, не хардкодяться.** Було 5 самописних `XIndexInitializer : IHostedService`, і ніщо не сигналізувало, що сервіс просто не написав свого. Тепер `IMongoIndexes` + `AddMongoIndexes<T>()`, а спільний `MongoIndexInitializer` реєструється **всередині `AddMongoDb`** — щоб сервіс без жодної декларації все одно отримав ініціалізатор і залогував warning. **Presence сьогодні цей warning і кидає** — індекси для `chat_memberships` навмисно лишені на scalability-раунд.

**[TL-120] Shared-шар = версіоновані NuGet-пакети.** Юзер обрав реальну розв'язку (не «задокументувати компроміс»). Contracts + 4 `src/shared/` мають PackageId/Version 1.0.0 і живуть у власному `TelegramLike.Shared.slnx`, **навмисно поза `TelegramLike.sln`** (project reference поруч переміг би версію пакета й зробив межу декоративною). Локальний feed — `artifacts/packages`, bootstrap — `build/pack-shared.ps1`.
- Пакувати треба **в порядку залежностей**: `Shared.Application` резолвить `Shared.Domain` як пакет, тож одним `dotnet pack` по slnx на холодному feed не вийде.
- ⚠️ **Головні граблі:** NuGet кешує розпакований пакет за id+version, тому перепак тієї ж версії лишає споживачів на попередньому білді — мовчки, як stale-publish у Docker. Скрипт евіктить лише наші id з `~/.nuget/packages` (ніколи `nuget locals --clear` — тут немає мережі для перезавантаження чужих пакетів).
- Dockerfile-и сервісів `COPY` feed перед restore → `docker compose build` без свіжого паку зашле stale shared-код.
- **НЕ перевірено:** збірка docker-образів (Docker не був запущений). Зміна у Dockerfile — тільки COPY, але не виконувалась.

**Порядок роботи був 2→3→4→1** навмисно: пункти 2 і 4 правлять код у shared-шарі, і робити це після заморозки версій означало б bump+repack на кожен крок.

**Ще НЕ зроблено (масштабованість, наступний раунд):** немає індексу на `messages {ChatId, SentAt}` (головний гарячий запит — COLLSCAN + in-memory sort, 32 МБ ліміт); `hidden_messages` читається цілком на кожну сторінку; `chat_memberships`/`chat_types` без індексів у messaging і presence; розмір `MessageSentIntegrationEvent` росте з розміром чату; пропускна здатність outbox ~25 подій/с на репліку (послідовний `FindOneAndUpdate` + послідовний publish); Web BFF stateful (sticky sessions); `ChatMembershipTracker` in-memory per-replica; **rate limiting відсутній скрізь** — ні на gateway, ні в сервісах.

**Why:** записано, бо це рішення й компроміси, яких не видно з коду — зокрема ЩО саме свідомо прийняли (2-секундне вікно fan-out, presence-warning) і ЧОМУ shared-шар поза основним рішенням.
**How to apply:** перед новим раундом — брати наступні пункти зі списку масштабованості, починаючи з індексів. Будь-яка зміна у shared-шарі = bump версії пакета + `./build/pack-shared.ps1`. Нова integration event = обов'язковий `[IntegrationEventName]`, інакше падає `IntegrationEventNamesTests`.
