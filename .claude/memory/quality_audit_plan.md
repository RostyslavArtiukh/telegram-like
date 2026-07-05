---
name: quality-audit-plan
description: "План на наступну сесію — повний аудит якості всього репо всіма агентами (баги, безпека, мертвий/устарілий код, дублювання, ненадійність) + виправлення"
metadata: 
  node_type: memory
  type: project
  originSessionId: d1782314-ee0f-4cee-8d00-ff7d0debd513
---

**Мета (сформульовано 2026-07-05 ввечері, на наступну сесію):** підняти якість максимально — повний наскрізний аудит **усього** проекту всіма агентами на: помилки/баги (correctness), безпеку, устарілі файли/папки/фічі (dead code, stale docs, залишки монолітної міграції), дубльований код, ненадійний код (races, fail-open, слабка обробка помилок). Потім виправити high-value безпечні знахідки з live-верифікацією.

Агенти в наявності: **reviewer, security, architect, tester, designer** (+ вбудований **Explore** для read-only sweep'ів). Per-service агентів свідомо видалили ([[permissions-preference]] нема, це у git-історії); директорні CLAUDE.md їх заміняють.

**Принципи (винесені з сесій TL-64..70):**
- Read-only аудити гнати **масово паралельно** (безпечно). Запис/фікси — серіалізувати або розводити по неперетинних областях: дві паралельні збірки того самого проєкту б'ються в obj/ (реальна гонка, ловили).
- Кожен фікс — **live-verify** (skill `verify`: E2E через gateway; CDP для MAUI; realtime-smoke), не лише build+test.
- **Гейт:** юзер затверджує backlog фіксів ДО виконання. Аудит спершу тільки читає.
- Не жаліти токени (юзер підтвердив).

**Фаза 0 — Prep (головна сесія, ~15 хв):**
- Інвентар репо: список проєктів, LOC по областях, кандидати-сироти (файли, не референсовані жодним csproj/sln; stale `docs/` — напр. `docs/plan.md` це 8-денний план; невживані memory-файли; залишки монолітної міграції). Baseline: повний `dotnet build` + `dotnet test` зелені, зафіксувати warnings. Формат знахідок: severity × area × effort — щоб зводилось.

**Фаза 1 — Паралельний read-only аудит (~9 агентів у фоні), розбито щоб не перетинались:**
- reviewer×4 (correctness/reliability/duplication по областях): **R1** SDK `src/client` + MAUI `src/app` + Contracts · **R2** Web BFF + gateway · **R3** services identity+notifications+presence · **R4** services chats+messaging+realtime.
- **security×1**: наскрізний auth/JWT/secret/authorization/input-validation sweep + відомі fail-open (Messaging retract/reaction/read, Presence typing, Realtime `JoinChat`).
- **architect×1**: структурний борг — мертві фічі/папки, устарілі патерни, дублювання між хостами (Web `ChatView.razor` vs MAUI `Chat.razor` — двічі писані; SDK), залишки міграції, версіонування Contracts, куди винести спільну логіку. Плюс infra (docker-compose/k8s/monitoring/CI/Dockerfiles).
- **tester×1**: аудит покриття — непокриті handlers/consumers/SDK, брак idempotency-тестів, flaky/skipped, Testcontainers-прогалини. Тільки ЗВІТ, не фіксити.
- **designer×1**: UI-аудит обох хостів — parity-gaps (у MAUI бракує reply/forward/hide, broadcast read-count, moderator retract — designer вже помітив), мертві компоненти, accessibility, надійність UX (loading/error/disposal).
- **Explore×1–2**: dead-code & staleness — файли без референсів, stale docs, orphaned config, TODO/HACK/FIXME, дубльовані блоки, устарілі memory-файли.

**Фаза 2 — Консолідація (головна сесія):** злити знахідки, здедупити (агенти перетнуться), ранжувати severity×effort×blast-radius → єдиний backlog: Fix-now-safe / Fix-with-care / Defer / Document-only. **Гейт: юзер обирає, що виконуємо.**

**Фаза 3 — Виконання (затверджений підмножина, verified):** батчами як у [TL-70] — безпечне спершу, серіалізовано/розведено. tester додає тести, designer — UI, я — код/infra. Live-verify кожен батч, коміт по логічній групі `[TL-N]`.

**Фаза 4 — Прибирання й доки:** видалити підтверджений мертвий код/папки/фічі; оновити stale docs + CLAUDE.md + memory. Фінальний build+test+E2E + звіт.

**Незапушеного нема** (origin/master на f160ac7 після TL-70). Стек compose лишався запущеним. Android SDK встановлено (adb працює) — телефон і глибокі рефактори (server-side isBroadcast/isModerator, hub-membership) відкладені окремо, не плутати з цим аудитом. Див. [[client-sdk-plan]], [[microservices-migration]].
