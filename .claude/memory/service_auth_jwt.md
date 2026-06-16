---
name: service-auth-jwt
description: JWT auth scheme між Web BFF і downstream сервісами — як додавати новий сервіс
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

> **ОНОВЛЕНО ([TL-43]…[TL-45], 2026-06-07):** Web більше **НЕ** issuer. Identity-сервіс став **IdP** і підписує access-токени (`iss=telegramlike-identity`), які валідують усі 5 сервісів. Web exchange'ить session token (cookie) на access-token через Identity (`ServiceTokenProvider`, scoped) і форвардить `Bearer`. `ServiceTokenIssuer`/`ServiceAuthHandler` у Web видалені. Деталі — [[microservices-migration]]. Нижче — історичний опис первісної схеми (Web як issuer, до екстракції Identity).

День 14 (2026-05-30): закрита auth-діра між Web BFF і Notifications-сервісом.

**Why:** до Дня 14 Web передавав `X-User-Id` header а downstream сервіси трактували як істину. У docker network будь-який контейнер міг підставити header і читати чужі нотифікації.

**Схема (HMAC-signed JWT):**
- Web (BFF) — issuer. Підписує JWT приватним секретом HMAC-SHA256.
- Notifications (resource server) — validator. Перевіряє підпис тим же секретом, валідує issuer/audience/exp.
- Shared secret у env var (НЕ commited на прод).
- Token claims: `sub` = userId (Guid string), `jti` = унікальний (для логування), `iat`/`exp`/`nbf` стандартні. Lifetime = 5 хв (короткий бо per-request).

**Файли:**
- Web:
  - [ServiceAuthOptions.cs](src/TelegramLike.Web/Services/NotificationsApi/ServiceAuthOptions.cs) — IOptions для `JwtSecret`/`Issuer`/`Audience`/`TokenLifetimeSeconds`
  - [ServiceTokenIssuer.cs](src/TelegramLike.Web/Services/NotificationsApi/ServiceTokenIssuer.cs) — Singleton, метод `IssueForUser(Guid userId)`
  - [ServiceAuthHandler.cs](src/TelegramLike.Web/Services/NotificationsApi/ServiceAuthHandler.cs) — DelegatingHandler, attach `Authorization: Bearer <jwt>`
  - [Program.cs](src/TelegramLike.Web/Program.cs) — реєстрація options + issuer + handler, прив'язка handler до `INotificationsApi` HttpClient
- Notifications.Api:
  - [Program.cs](src/services/notifications/TelegramLike.Notifications.Api/Program.cs) — `AddAuthentication(JwtBearerDefaults).AddJwtBearer(...)`, group `/notifications` має `.RequireAuthorization()`, `TryGetUserId` читає `sub` з `httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)`
  - **Важливо:** `options.MapInboundClaims = false` — інакше .NET автоматично переіменовує `sub` → `ClaimTypes.NameIdentifier` і код плутається.

**Config (appsettings + docker env):**
```
ServiceAuth__JwtSecret = <base64 384-bit secret>
ServiceAuth__Issuer = "telegramlike-web"
ServiceAuth__Audience = "telegramlike-services"
ServiceAuth__TokenLifetimeSeconds = 300   # тільки для Web (issuer)
```

`ClockSkew = 30s` у validation — захист від невеликих розбіжностей часу між контейнерами.

**Як додати auth для нового сервісу (рецепт):**
1. У новому сервісі (`<Service>.Api`):
   - Додати NuGet `Microsoft.AspNetCore.Authentication.JwtBearer`
   - У `Program.cs`: скопіювати `AddAuthentication().AddJwtBearer(...)` блок з Notifications.Api
   - `app.UseAuthentication(); app.UseAuthorization();`
   - На endpoint group: `.RequireAuthorization()`
   - У endpoint: `httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value`
   - Додати ту ж `ServiceAuth__*` секцію в appsettings.json + docker env
2. У Web:
   - Створити typed HttpClient для нового сервісу (за паттерном `INotificationsApi`)
   - При реєстрації: `.AddHttpMessageHandler<ServiceAuthHandler>()` — handler **той самий**, працює з будь-яким downstream
3. Той же shared `JwtSecret` обов'язково однаковий у Web і у новому сервісі.

**Verify:**
- `curl http://localhost:8081/notifications/unread-count` → `401` (без token)
- `curl http://localhost:8081/health` → `200` (public endpoint)
- Через UI — нотифікації показуються (UserAccessor → Issuer → handler → bearer → JwtBearer validate → handler reads sub)

**Threat model (що цей дизайн НЕ покриває):**
- Якщо secret витече — все компроментоване. Для прода: secret manager + rotation.
- Web сам є trust boundary — якщо Blazor session compromised, attacker отримає валідні токени.
- Немає revocation: токен дійсний до exp навіть якщо юзер логаут. Виправляється коротким TTL (5хв).
- Service-to-service: ще не закрито інші сервіси (Presence/Identity у monolith) — додавати поетапно.

**Тести (TODO):**
- Unit на `ServiceTokenIssuer` — корректно сетить claims/exp
- Integration на `Notifications.Api` — реальний request з/без token, з простроченим token, з невалідним підписом
