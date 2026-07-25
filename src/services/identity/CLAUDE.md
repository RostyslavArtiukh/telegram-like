# Identity service — the IdP (port 8085, DB `telegramlike_identity`)

Owns users + authentication, and **signs the access JWTs every other service trusts**. 4 projects (Domain/Application/Infrastructure/Api), namespace `TelegramLike.Identity.*`. **No RabbitMQ / no outbox** — Identity publishes no integration events.

## Endpoints
- **Public (no bearer — these bootstrap auth):** `POST /auth/register`, `POST /auth/login` (→ session token), `POST /auth/token` (session token → `{userId, username, email, accessToken, expiresInSeconds}`), `POST /auth/logout` (revoke a session token → `204`; idempotent, unknown/expired token is a no-op — possession of the token is the credential, same as `/token`).
- **Authed (validates its own `iss=telegramlike-identity` token):** `GET /users/{id}`, `POST /users/by-ids`, `GET /users/by-username?u=`.
- **Controllers (`Controllers/`):** `AuthController` (`[AllowAnonymous]`, the bootstrap trio) + `UsersController` (authed lookups), on `ApiControllerBase`; `DomainExceptionFilter` keeps the legacy `400 {error}` body (`ValidationException`/`DomainException`) the Web BFF client reads — **not** `ProblemDetails`. Handlers **and the value-object guards** (Username/Email/DisplayName/HashedPassword, migrated in [TL-98]) throw `DomainException` → 400; framework exceptions stay a 500. See the `api_controllers` memory.

## Key pieces
- `AccessTokenIssuer` (Infrastructure) — HMAC-SHA256, `iss=telegramlike-identity`, claims `sub`/`jti`, lifetime = `ServiceAuth:TokenLifetimeSeconds`.
- `ExchangeSessionQuery` — session token → user + minted access token (keeps the Api endpoints thin).
- `RedisSessionService` — opaque `session:{token}` in Redis = the durable browser credential, TTL `Auth:SessionTokenTtlDays`. Logout deletes the key (`EndSessionCommand`) so it can't mint further access JWTs; already-issued JWTs still lapse on their own short lifetime (services validate statelessly, never hit Redis).
- `BcryptPasswordHasher`; `UserRepository` over Mongo `users`.
- Handlers throw `DomainException` / FluentValidation `ValidationException` → Api maps both to `400 {error}`.

## Don't
- Don't add a message bus unless a real Identity integration event appears.
- Keep `register` / `login` / `token` **public**; everything else `.RequireAuthorization()`.
- Same shared `ServiceAuth:JwtSecret` as all other services — changing it breaks every token.
