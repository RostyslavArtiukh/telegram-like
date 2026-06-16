# Identity service — the IdP (port 8085, DB `telegramlike_identity`)

Owns users + authentication, and **signs the access JWTs every other service trusts**. 4 projects (Domain/Application/Infrastructure/Api), namespace `TelegramLike.Identity.*`. **No RabbitMQ / no outbox** — Identity publishes no integration events.

## Endpoints
- **Public (no bearer — these bootstrap auth):** `POST /auth/register`, `POST /auth/login` (→ session token), `POST /auth/token` (session token → `{userId, username, email, accessToken, expiresInSeconds}`).
- **Authed (validates its own `iss=telegramlike-identity` token):** `GET /users/{id}`, `POST /users/by-ids`, `GET /users/by-username?u=`.

## Key pieces
- `AccessTokenIssuer` (Infrastructure) — HMAC-SHA256, `iss=telegramlike-identity`, claims `sub`/`jti`, lifetime = `ServiceAuth:TokenLifetimeSeconds`.
- `ExchangeSessionQuery` — session token → user + minted access token (keeps the Api endpoints thin).
- `RedisSessionService` — opaque `session:{token}` in Redis = the durable browser credential, TTL `Auth:SessionTokenTtlDays`.
- `BcryptPasswordHasher`; `UserRepository` over Mongo `users`.
- Handlers throw `InvalidOperationException` / FluentValidation `ValidationException` → Api maps both to `400 {error}`.

## Don't
- Don't add a message bus unless a real Identity integration event appears.
- Keep `register` / `login` / `token` **public**; everything else `.RequireAuthorization()`.
- Same shared `ServiceAuth:JwtSecret` as all other services — changing it breaks every token.
