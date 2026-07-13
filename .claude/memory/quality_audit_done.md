---
name: quality-audit-done
description: Наскрізний аудит якості всього репо (9 агентів) + фікси TL-71..82 — ЗРОБЛЕНО; що виправлено і що свідомо відкладено
metadata: 
  node_type: memory
  type: project
  originSessionId: 2aa8943c-07a6-40a6-b516-fbf7b635dd45
---

Наскрізний аудит якості (баги/безпека/надійність/dead-code/дублювання/покриття) 9 агентами (reviewer×4, security, architect, tester, designer, Explore) + виправлення. Виконано 2026-07-06/07 як [TL-71..82] (16 комітів). Замінює план [[quality-audit-plan]] (той — pre-execution).

**Baseline на старті:** 156 тестів. **Після:** 343 тести, 18 проєктів, зелено; повний `dotnet build` 0 warnings. *(Пізніше [TL-95..97] тест-проєкти злито 18→**8**, тестів ~**361**, нейминг уніфіковано.)*

**Ключова рамка безпеки, яку встановив аудит:** зовнішні SDK/MAUI-клієнти б'ють у gateway **напряму**, минаючи Web BFF — тому всі «перевірка живе в BFF» перестали бути контролем. Звідси весь IDOR-кластер.

## Що виправлено (по батчах)
- **TL-71 Identity:** унікальні індекси email/username + переклад dup-key (race у register); гейт `AccountStatus.Active` на login+ExchangeSession (banned/deleted більше не автентифікуються); case-insensitive uniqueness (collation).
- **TL-72 Presence:** «online назавжди» після закриття браузера (Redis-miss→Offline); пропущений `UserCameOnline` на реконекті (Redis авторитетний, перевірка ДО touch); read-model ordering (IsActive+LastEventAt, LWW по OccurredAt, soft-deactivate); typing через sorted-set замість KEYS/SCAN; CS0618 Testcontainers.
- **TL-73 Notifications:** індекси feed `(RecipientId,CreatedAt,_id)`+`(RecipientId,Status)`; skip publish коли ModifiedCount==0 (mark-read); fanout публікує UnreadCount **незалежно** від inserted-count (інакше сигнал губився назавжди при fail-after-insert).
- **TL-74 Messaging:** read-IDOR (GetChatMessages 403 / GetMessageById 404 через read-model); unique index на read-receipts; broadcast read idempotent+atomic ($inc раз на reader); read-model ordering; зареєстровано FluentValidation ValidationBehavior (був мертвий).
- **TL-74b Messaging:** реакції/retract lost-update → **optimistic concurrency** (`Message.Version` + `ConcurrencyRetry`); **retract-moderator тепер сервер-сайд** — read-model матеріалізує `Role` (з `MemberJoined`+новий `MemberRoleChanged` integration event), `IsModeratorAsync`; client-флаг `actorIsModerator` ігнорується.
- **TL-75 Chats:** read-IDOR на GetChatById(404)/GetChatMembers(403) через `IsActiveMemberAsync`; **outbox**: claim/lease 60с (дублювання publish під >1 репліку) + стабільне ім'я типу замість AssemblyQualifiedName. Застосовано до обох копій (chats+messaging).
- **TL-76 Realtime:** JoinChat перевіряє membership через **in-memory event-sourced tracker** (fail-closed для відомих чатів, fail-open для невідомих — realtime без БД).
- **TL-77 Infra:** k8s realtime (25-realtime.yaml + kustomization + gateway destination — раніше цілком відсутній під k8s); CI job build-maui (windows-latest, App.slnx — раніше не білдився ніде); Contracts versioning convention у CLAUDE.md.
- **TL-78 SDK:** FlushJoinsAsync per-join try/catch (реконект не кидав решту); HideLastSeen honored; session UserId/Username через volatile immutable ref (torn read).
- **TL-79 Web BFF:** Home.razor try/catch + ErrorBoundary (transient outage клав circuit); off-dispatcher StateHasChanged фікс (realtime msgs не позначались read); NavMenu interactive island (badge був мертвий) + icon CSS; typing ConcurrentDictionary; broaden login catch; session-token у URL→POST form; signout CSRF; cookie Secure/SameSite; a11y (real buttons, aria-live); NotFound.
- **TL-80 MAUI:** parity з Web — reply/forward/hide/attachments/manage-panel/notifications-page/broadcast-create/direct-online + moderator/premium; guard init; StopTyping; online filter; a11y. Білдиться (net-windows).
- **TL-81 Tests:** +7 нових тест-проєктів (Messaging Domain/App/Infra, Chats Domain/App, Identity App, Gateway) + realtime tracker; регресії на кожен security-фікс. 156→343.
- **TL-82:** видалено монолітні husk-теки `src/TelegramLike.{Domain,Application,Infrastructure}` + junk `monitoring/rules.yml;C`; забанеровано stale docs; виправлено drift у CLAUDE.md; задокументовано JWT-секрет.

## Свідомо відкладено / не роблено (рішення юзера)
- **B1 JWT-секрет — тільки задокументовано** (не ротовано): committed dev-default, симетричний HMAC = ключ валідації. ОК для локалки; для реального деплою — свіжий секрет лише через env/secret. Записано у кореневому CLAUDE.md.
- **C8** dedup `RedactAccessTokenProcessor` (gateway↔realtime) — лишено: злиття зв'язало б два окремі деплойники заради ~13 рядків.
- **C13** прибирання vestigial actor-id параметрів SDK — breaking, відкладено.
- Fail-open (deferred, документовано в messaging/CLAUDE.md): `AddReaction`/`MarkAsRead` не реджектять не-членів (log-only); `isBroadcast`/`isPremium` ще caller-supplied (нема type/premium у read-model). Presence typing fail-open.

**origin/master:** усе запушено (перевірено 2026-07-13 — working tree чистий, `master` == `origin/master`).
