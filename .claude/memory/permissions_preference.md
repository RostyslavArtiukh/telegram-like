---
name: permissions-preference
description: Користувач не хоче постійно підтверджувати команди — діяти проактивно у межах .claude/settings.local.json allowlist
metadata:
  node_type: memory
  type: feedback
  originSessionId: 62cc2e6a-d21e-47f2-9647-8294fc3dff38
---

Користувач втомився від permission prompts і налаштовує `.claude/settings.local.json` з широким allowlist (dotnet *, docker *, curl *, taskkill *, PowerShell *, тощо).

**Why:** pet-проект, локальна машина, він довіряє діям у межах цього репо. Витрачає час на ручне підтвердження кожного `dotnet test` або `docker ps`.

**How to apply:**
- Не питати додаткових підтверджень для рутинних операцій у межах `d:\projects\Practice\TelegramLike`: build/test/run, docker compose, curl до localhost, taskkill власних процесів, MongoDB shell exec.
- Все одно ПИТАТИ перед: `git push`, `git reset --hard`, `rm -rf` поза проектом, `docker system prune`, будь-якими операціями що affect shared state (PR, push, deploy).
- Шпаргалка з готовим JSON для permissions: [.claude/claude-permissions.txt](file:///d:/projects/Practice/TelegramLike/.claude/claude-permissions.txt).
- Якщо нова команда блокується — спочатку виконати, побачити reject, потім запропонувати додати її до settings.local.json одним абзацом, а не питати на кожен запуск.

## Повний дозвільний/заборонний список (зі шпаргалки)

**Allow:**
- `Skill(*)` — усі skills
- `Bash(dotnet *)`, `Bash(docker *)`, `Bash(docker compose *)`, `Bash(curl *)`
- `Bash(grep *)`, `Bash(find *)`, `Bash(cat *)`, `Bash(ls *)`, `Bash(rm *)`, `Bash(echo *)`
- `Bash(taskkill *)`
- Git read-only: `Bash(git status)`, `Bash(git log *)`, `Bash(git diff *)`, `Bash(git show *)`, `Bash(git branch *)`
- PowerShell: `Get-Process *`, `Stop-Process *`, `Get-ChildItem *`, `Get-Content *`, `Test-Path *`
- WebFetch domains: learn.microsoft.com, docs.mongodb.com, dotnet.microsoft.com, github.com, stackoverflow.com

**Deny (потребує явного запиту користувача):**
- `Bash(git push *)` — explicit consent needed every time
- `Bash(git reset --hard *)`
- `Bash(rm -rf /*)` — root-level deletion
- `Bash(docker system prune *)`

## Поведінкові режими (з docs)
- `acceptEdits` — авто-схвалення edit
- `bypassPermissions` — повний bypass (підходить для sandbox/VM)
- `claude --dangerously-skip-permissions` — те ж саме на запуск
