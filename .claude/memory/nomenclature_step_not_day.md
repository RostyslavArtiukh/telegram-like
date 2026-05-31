---
name: nomenclature-step-not-day
description: "Маркувати ітерації як \"Step N\" а не \"Day N\" — \"Day\" плутає бо за один фізичний день буває кілька кроків"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

Використовувати **"Step N"** замість "Day N" для нових ітерацій (нумерація продовжує існуючу — наступний буде Step 22 після Day 1-21 у plan.md та git history).

**Why:** один фізичний день містить кілька логічних кроків (Days 17-21 усі датовані 2026-05-30). "Day N" створює враження що це реальний день, а це фактично завдання/ітерація. "Step" нейтрально і чесно.

**How to apply:**
- Нові секції у `docs/plan.md` — "## Step N (YYYY-MM-DD): <тема> ✅"
- Git commit subject — "Step N: <тема>" замість "Day N: <тема>"
- Conversational відповіді у чаті — "Step 22 — distributed tracing" тощо
- НЕ перейменовувати існуючі Day 1-21 — git history їх вже містить як "Day", переписувати = breakage
- Memory entries про вже зроблену роботу (наприклад "День 12 (2026-05-24)" у `project_status.md`) також лишити як є — це history snapshot

**Continuity:** Numbering продовжується. Day 21 → Step 22 → Step 23. Один лічильник, дві назви через історичну причину.
