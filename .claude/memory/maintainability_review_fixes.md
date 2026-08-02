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

**[TL-119] Mongo-індекси декларуються, не хардкодяться.** Було 5 самописних `XIndexInitializer : IHostedService`, і ніщо не сигналізувало, що сервіс просто не написав свого. Тепер `IMongoIndexes` + `AddMongoIndexes<T>()`, а спільний `MongoIndexInitializer` реєструється **всередині `AddMongoDb`** — щоб сервіс без жодної декларації все одно отримав ініціалізатор і залогував warning. **Presence тоді цей warning і кидав** — індекси для `chat_memberships` навмисно лишили на scalability-раунд, закрито в [TL-123].

**[TL-120] Shared-шар = версіоновані NuGet-пакети.** Юзер обрав реальну розв'язку (не «задокументувати компроміс»). Contracts + 4 `src/shared/` мають PackageId/Version 1.0.0 і живуть у власному `TelegramLike.Shared.slnx`, **навмисно поза `TelegramLike.sln`** (project reference поруч переміг би версію пакета й зробив межу декоративною). Локальний feed — `artifacts/packages`, bootstrap — `build/pack-shared.ps1`.
- Пакувати треба **в порядку залежностей**: `Shared.Application` резолвить `Shared.Domain` як пакет, тож одним `dotnet pack` по slnx на холодному feed не вийде.
- ⚠️ **Головні граблі:** NuGet кешує розпакований пакет за id+version, тому перепак тієї ж версії лишає споживачів на попередньому білді — мовчки, як stale-publish у Docker. Скрипт евіктить лише наші id з `~/.nuget/packages` (ніколи `nuget locals --clear` — тут немає мережі для перезавантаження чужих пакетів).
- Dockerfile-и сервісів `COPY` feed перед restore → `docker compose build` без свіжого паку зашле stale shared-код.
- **Перевірено на живому стеку (2026-08-02):** усі 8 образів збираються, restore всередині контейнера тягне shared-пакети зі скопійованого feed, стек піднімається healthy.


**Live-верифікація раунду (2026-08-02, після підняття Docker):** 566 тестів (з Testcontainers) зелені; 8 образів зібрано; стек healthy. Рантайм-підтвердження: [TL-119] кожен сервіс логує «Indexes ensured for X», presence — саме той warning; [TL-117] нові рядки outbox мають `chats.chat-created.v1` / `messaging.message-sent.v1`, а **синтетичний pending-рядок зі старим CLR-ім'ям опублікувався з 0 retries** — тобто fallback реально робить деплой безпечним без дренажу черги; у `/metrics` видно обидві форми лейбла `event_type` (нові — як є, legacy — вкорочений до `MemberJoinedIntegrationEvent`); [TL-118] `POST /messages` без поля recipients → у payload `MessageSent` рівно id другого учасника, unread-count у нього став 1.

**Порядок роботи був 2→3→4→1** навмисно: пункти 2 і 4 правлять код у shared-шарі, і робити це після заморозки версій означало б bump+repack на кожен крок.

**Список масштабованості (8 стель) — ЗРОБЛЕНО окремим раундом [TL-121..128], див. [[scalability-ceilings-fixed]].** Був: індекс на `messages {ChatId, SentAt}`; `hidden_messages` цілком на кожну сторінку; `chat_memberships`/`chat_types` без індексів; розмір `MessageSentIntegrationEvent` росте з розміром чату; пропускна здатність outbox ~25 подій/с на репліку; Web BFF stateful; `ChatMembershipTracker` in-memory per-replica; rate limiting відсутній скрізь. Там же — що з цього прийняли як компроміс і чого не робили.

**Why:** записано, бо це рішення й компроміси, яких не видно з коду — зокрема ЩО саме свідомо прийняли (2-секундне вікно fan-out, presence-warning) і ЧОМУ shared-шар поза основним рішенням.
**How to apply:** перед новим раундом — брати наступні пункти зі списку масштабованості, починаючи з індексів. Будь-яка зміна у shared-шарі = bump версії пакета + `./build/pack-shared.ps1`. Нова integration event = обов'язковий `[IntegrationEventName]`, інакше падає `IntegrationEventNamesTests`.
