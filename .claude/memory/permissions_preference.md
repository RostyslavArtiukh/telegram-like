---
name: permissions-preference
description: Standing grant — full Bash + PowerShell command access in this project, every session, no per-use approval; enforced via .claude/settings.local.json
metadata:
  node_type: memory
  type: feedback
  originSessionId: 62cc2e6a-d21e-47f2-9647-8294fc3dff38
---

**Standing grant (2026-06-17):** the project owner gave full, durable permission to run **all** shell commands (Bash + PowerShell) in this project, **every session, with no per-use approval**. His permission is no longer required to run commands here.

**Enforced in** `.claude/settings.local.json`: tool-only allow rules `"Bash"` and `"PowerShell"` (name-only rule = allow everything for that tool) plus `"defaultMode": "bypassPermissions"`. The long list of specific `Bash(...)`/`PowerShell(...)` entries below them is now redundant (superseded by the broad rules) — harmless, can be tidied later.

**Why:** pet project on his local machine; he trusts actions within this repo and was tired of approving each `dotnet test` / `docker` / `git` call.

**How to apply:**
- Don't ask permission to run commands in this project — just run them. This includes `git add`/`commit`/`push` (exercised freely this session).
- Still **state intent** briefly before genuinely destructive or outward-facing actions (push, force-reset, deploy, deleting things I didn't create) — surface, don't gate. He waived the *prompt*, not the *transparency*.
- This lives in `settings.local.json` (local, gitignored) — personal to this machine, not imposed on collaborators via the checked-in `settings.json`.

**Superseded:** the earlier "ask before `git push`" / deny-list caveat no longer applies — push is allowed and routine here.
