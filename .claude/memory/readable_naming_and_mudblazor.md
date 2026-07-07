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
- DDD-терміни в бекенді (aggregate/repository/handler/command) мають доменний сенс — уточнювати з юзером, наскільки глибоко перейменовувати бекенд (не тільки UI). Пов'язано з [[quality-audit-done]].
