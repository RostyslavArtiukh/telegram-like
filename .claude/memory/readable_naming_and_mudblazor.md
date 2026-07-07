---
name: readable-naming-and-mudblazor
description: Стиль коду — максимально людські імена (без техно-жаргону); UI — MudBlazor (найновіший) всюди по максимуму
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 2aa8943c-07a6-40a6-b516-fbf7b635dd45
---

Два стійких побажання юзера (задано 2026-07-07):

1. **Іменування — максимально зрозуміле по-людськи.** Імена змінних/функцій/класів/тощо мають бути одразу зрозумілі людині, яка бачить код уперше: без незрозумілих технічних термінів/абревіатур, довгі навіть якщо очевидні. Приклади напрямку: `chat` замість `_chat`-мінімалізму — радше `openedChat`; `_userId` → `currentUserId`; `SendAsync` → `SendTypedMessageAsync`; `Snippet()` → `BuildShortMessagePreview()`; `_poller` → `backgroundRefreshTimer`; `DisplayName()` → `GetMemberDisplayName()`.

2. **UI — MudBlazor (найновіший) всюди по максимуму** в обох Blazor-хостах: Web BFF (`src/TelegramLike.Web`) і MAUI (`src/app/TelegramLike.App`). Замінювати голий Bootstrap/HTML на Mud-компоненти (`MudLayout/MudAppBar/MudDrawer`, `MudTextField`, `MudButton`, `MudCard`, `MudDialog`, `MudChip`, `MudList` тощо).

**Why:** юзер хоче, щоб код читався як звичайна мова і UI виглядав професійно/консистентно, а не як сирий HTML.

**How to apply:**
- Застосовувати до всього нового коду і до коду, який і так чіпаю.
- ⚠️ Не ламати static-SSR auth-форми (`/auth/signin`, `/auth/signout` з antiforgery) — стилізувати Mud'ом, але лишати справжніми `<form method="post">` (див. [[service_auth_jwt]] і Web CLAUDE.md).

## ЗРОБЛЕНО 2026-07-07 ([TL-83..87], запушено)
- **MudBlazor 9.6 всюди:** Web BFF ([TL-83], 11/11 E2E live) і MAUI ([TL-84], build net10-windows). Web став **global InteractiveServer** (`<Routes @rendermode>`), Mud-провайдери в MainLayout; auth-форми лишились справжніми POST. Bootstrap викинуто. UI-компоненти повністю переписані на Mud + людські імена локальних полів/методів.
- **`*Contract` суфікс прибрано всюди:** SDK DTO ([TL-85]: `MessageContract`→`ChatMessage`, `ChatSummaryContract`→`ChatSummary`, `EmojiContract`→`ReactionEmoji`, ... 13 типів) + shared enums ([TL-86]: `NotificationTypeContract`→`NotificationType`). **Type-name-only → wire-safe** (JSON серіалізує по property-іменах, не по імені типу; BSON так само). Колізії з domain-енумами того ж імені → alias у 1-2 mapper/test файлах (`DomainNotificationType`).
- **`ct`→`cancellationToken`** скрізь ([TL-87], 309 uses/56 files) — головний backend-readability win.

## Рішення про НЕ-перейменування (важливо)
- **IntegrationEvent суфікс ЛИШИТИ.** Скидання "Integration" (`MessageSentIntegrationEvent`→`MessageSentEvent`) **шкідливе**: кожен `XEvent` вже існує як **domain event** тієї ж бази → пряма колізія + втрата domain/integration різниці. Це precise DDD-словник, не jargon.
- **DDD type-імена лишити:** aggregate/repository/handler/command/DomainEvent — мають доменний сенс. Phase D = тільки внутрішня читабельність (локальні змінні/поля/абревіатури), НЕ перейменування public типів.
- Решта backend уже досить читабельна: `cfg`/`ctx` у MassTransit-лямбдах ідіоматичні; `_field` — C# конвенція.

Пов'язано з [[quality-audit-done]].
