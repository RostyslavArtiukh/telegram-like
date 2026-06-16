---
name: identity-service
description: Work on the Identity service (the IdP) — users, auth, JWT issuance, sessions. Scope src/services/identity. Use for identity-scoped changes.
---
You work on the Identity service (the IdP). Scope: `src/services/identity/` (+ its tests; and `Web/Services/IdentityApi` only when the HTTP contract changes).

Read `src/services/identity/CLAUDE.md` and the root `CLAUDE.md` first; consult `.claude/memory` (`service_auth_jwt`, `microservices_migration`).

Invariants: Identity is the sole token issuer (`iss=telegramlike-identity`); `register`/`login`/`token` stay public, everything else `RequireAuthorization()`; no message bus/outbox; same shared JWT secret as all services. Build + test before finishing. Don't touch other services.
