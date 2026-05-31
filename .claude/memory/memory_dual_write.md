---
name: memory-dual-write
description: "Memory files mirrored до .claude/memory/ у репо для синхронізації між машинами через git"
metadata:
  type: feedback
---

При **створенні або оновленні будь-якого memory файлу** (включно з `MEMORY.md`) — записувати у **обидва місця**:

1. `C:\Users\Ros\.claude\projects\d--projects-Practice-TelegramLike\memory\<name>.md` (глобальна папка яку читає Claude Code harness)
2. `d:\projects\Practice\TelegramLike\.claude\memory\<name>.md` (локальна копія у репо, комітиться в git)

**Why:** Юзер працює з двох машин (десктоп + ноутбук). Глобальна папка Claude Code — per-machine, не синхронізується. Дзеркало у репо їде разом з кодом, тому контекст переноситься через `git pull`.

**How to apply:**
- Кожен `Write` на memory файл = два `Write` (один в global, один в repo)
- При delete — теж видаляти з обох
- На новій машині після `git clone` юзер копіює `.claude/memory/*.md` у свою глобальну Claude Code memory папку для свого юзернейму, щоб harness її побачив
- `.claude/memory/` НЕ в `.gitignore` — тільки `.claude/settings.local.json`
