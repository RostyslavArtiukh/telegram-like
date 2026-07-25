---
name: error-handling-domain-exceptions
description: "Конвенція помилок у сервісах — кидати DomainException/ForbiddenException, НЕ сирі BCL-винятки; DomainExceptionFilter + Phase A логування/traceId"
metadata: 
  node_type: memory
  type: project
  originSessionId: 3bae5ccb-f88d-4ed8-82a7-2ad0af4247d7
---

> **Оновлення [TL-98] (2026-07-13):** Phase B доведена до кінця в решті сервісів — identity (value-object guard-и Username/Email/DisplayName/HashedPassword + `User.Register`), notifications (`Notification`/`NotificationPayload` + `MarkAll*/MarkChat*`-хендлери), presence (`UserPresence` + Heartbeat/GoOffline) тепер кидають `DomainException` → 400; presence-фільтр більше **не** no-op (`ForbiddenException`→403 / `DomainException`→400, ProblemDetails+traceId). Свідомо лишені сирими (data-integrity/config → 500 правильний): fanout enum-default guard у Notifications, `ContractMappers` unknown-type, config-guard-и у Program.cs/InfrastructureSetup, десеріалізаційні guard-и `ChatRepository`. Уточнення: з [TL-94] `DomainException`/`ForbiddenException` — **спільні** типи в `TelegramLike.Shared.Domain` (per-service копії, описані нижче, видалені). Актуальні мапінги фільтрів — див. [[api_controllers]]. Додано `DomainExceptionFilterTests` + WebApplicationFactory-harness для identity/notifications/presence (за зразком chats/messaging).

Крок (2026-07-08): двофазне покращення обробки помилок у 5 сервісах (за запитом «надто проста обробка»).

**Phase A (безпечно, без зміни контракту):** у 4 `DomainExceptionFilter` (identity/notifications/chats/messaging) додано `ILogger` + `traceId` (`Activity.Current?.TraceId` → fallback `HttpContext.TraceIdentifier`). ProblemDetails-фільтри кладуть `traceId` в `Extensions`; Identity лишає `{error}`-тіло, лог тільки. Presence — no-op, не чіпали. Раніше змаповані 4xx ковталися тихо (невидимі в логах/трейсах).

**Phase B (зміна протестованого контракту, підтверджено юзером):** доменна ієрархія винятків замість ловлі сирих BCL-типів.
- **Дефект, який лагодимо:** фільтри мапили *сирі* `InvalidOperationException`/`ArgumentException`→400 і клали `ex.Message` у тіло. Але ці типи кидає й фреймворк (LINQ `.Single()`, Mongo-драйвер, `ChatRepository` «Unknown chat type», `ContractMappers` «Unknown notification type») → баг сервера мовчки ставав клієнтським 400 з витоком внутрішнього повідомлення.
- **Рішення:** кожен сервіс має власний `DomainException(string)` у **своєму Domain** (Domain лишається чистим, HTTP-агностичним — без залежності на Api, без shared-kernel). chats+messaging додатково мають `ForbiddenException : DomainException` (для 403). Фільтр мапить ці **семантичні** типи; будь-що інше (зокрема сирі BCL) → 500.
- **Конвенція далі:** у Domain/Application/Infrastructure кидати `DomainException`/`ForbiddenException`, **НЕ** `throw new InvalidOperationException/ArgumentException/UnauthorizedAccessException`. Domain HTTP-кодів не знає — мапінг у фільтрі.
- **Per-service мапінг (поважає старі контракти, кожен фільтр різний навмисно):**
  - chats: `ForbiddenException`→403, `DomainException`→400, ProblemDetails.
  - messaging: `ValidationException`(FluentValidation)→400, `ForbiddenException`→403, `DomainException`→400.
  - identity: `ValidationException`→400 (склеєні), `DomainException`→400, тіло `{error}` (не ProblemDetails). Його `ArgumentException` value-object guard-и **навмисно НЕ конвертовані** → лишаються 500 (як і було). Бізнес-правило дубль-email у `UserRepository` (Infrastructure) → `DomainException` (лишається 400).
  - notifications: `DomainException`→400 only. `ArgumentException` та `ContractMappers` unknown-type лишаються 500.
  - presence: no-op (усе→500), без змін.
- Конверсія Domain+Application була механічна (sed по каталогах, поважаючи що саме сервіс мапив), Infrastructure переважно лишено (data-integrity правильно стає 500).

**Характеризаційні тести:** `ChatsDomainExceptionFilterTests`/`MessagingDomainExceptionFilterTests` переписані під новий контракт (кидають `DomainException`/`ForbiddenException`; додано тест, що сирий `InvalidOperationException` НЕ мапиться в 400). Доменні/Application тести (`Throw<InvalidOperationException/ArgumentException/UnauthorizedAccessException>`) оновлені на `Throw<DomainException/ForbiddenException>`. Уся тестова база зелена (усі сервіси + Client/Gateway/Realtime + Infrastructure через Testcontainers).

**Не зроблено/відкладено:** плюмбінг фільтрів досі частково дублюється (кожен per-service через різні тіла/таблиці) — свідомо не уніфіковано в один фільтр, бо розбіжності реальні (Identity `{error}`, Presence no-op). Пов'язано з [[service_auth_jwt]] (та ж хвиля рефакторингу Api-шару + спільний `TelegramLike.Shared.Api`).
