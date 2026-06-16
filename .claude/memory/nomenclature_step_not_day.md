---
name: nomenclature-step-not-day
description: "Префікс комітів/ітерацій — поточне правило [TL-N] (Jira-style). Еволюція: Day N → Step N → [TL-N], один running counter"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

Поточне правило (з 2026-06-07): маркувати нові ітерації/коміти префіксом **`[TL-N]`** — напр. `[TL-43] <тема>`. Номер — той самий running counter, що й раніше.

**Еволюція конвенції (один лічильник, різні префікси через історію):**
- **Day 1–21** — найперша назва.
- **Step 22–42** — "Day" плутав (за один фізичний день буває кілька кроків), перейшли на "Step".
- **[TL-43]+ — поточне.** Юзер попросив (2026-06-07) Jira-style ключ `[TL-N]` замість слова "Step"/"Day".

**Why:** `[TL-N]` компактний, схожий на трекер-key (TL = TelegramLike), легко грепати/лінкувати. "Day"/"Step" прозовіші й плутаються.

**How to apply:**
- Git commit subject — `[TL-N] <тема>` (напр. `[TL-43] Identity extraction — Phase 5 (Web BFF rewiring)`).
- Нові секції у `docs/plan.md` і відповіді в чаті — теж `[TL-N]` для консистентності.
- **НЕ перейменовувати** існуючі Day 1–21 / Step 22–42 — git history їх містить, переписувати = breakage. History snapshot лишається як є.
- Housekeeping-коміти (memory/docs sync) — без номера, звичайний `docs: …` (як раніше).
- Наступний номер після Step 42 → **`[TL-43]`** (Identity extraction Phase 5).

**Continuity:** Day 21 → Step 22 → … → Step 42 → [TL-43]. Один лічильник, три префікси через історичну причину.
