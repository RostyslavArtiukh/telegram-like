---
name: service-auth-jwt
description: JWT auth між клієнтами і сервісами — Identity є IdP; спільний AddServiceJwtAuth + ApiControllerBase у TelegramLike.Shared.Api
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

**Актуальна схема (2026-07, після [TL-43..45] і [TL-92]):**

- **Identity — IdP.** Підписує короткоживучі HMAC-SHA256 JWT: `iss=telegramlike-identity`, `aud=telegramlike-services`, `sub`=userId (Guid string), lifetime ~5 хв. **Web (BFF) нічого не підписує** — тримає cookie-сесію і міняє opaque session token на access JWT в Identity (`ServiceTokenProvider`, scoped, імплементує SDK-шний `IAccessTokenProvider`), далі `Bearer` на всіх downstream викликах. Standalone-клієнти (SDK/MAUI) роблять те саме через `TelegramLikeSession` (login → session token → exchange → кешований JWT з refresh-before-expiry).
- **Валідація — спільна ([TL-92]).** `src/shared/TelegramLike.Shared.Api`:
  - `ServiceAuthExtensions.AddServiceJwtAuth(IConfiguration)` — єдине джерело `AddAuthentication().AddJwtBearer(...)` + `AddAuthorization`. Читає `ServiceAuth:JwtSecret/Issuer/Audience`; `MapInboundClaims=false` (**критично** — інакше .NET переіменовує `sub` → `ClaimTypes.NameIdentifier`); `ClockSkew = 30s` (розбіжності часу між контейнерами). Раніше цей блок був дослівно скопійований у 5 `Program.cs`.
  - `ApiControllerBase` — резолвить `CurrentUserId` через `IActionFilter` раз на запит, віддає 401 до тіла екшену; **пропускає `[AllowAnonymous]`** (`EndpointMetadata.OfType<IAllowAnonymous>()`) — без цього Identity register/login/exchange ламались 401.
  - Підключення в сервісі: `<ProjectReference>` на shared + `<Using Include="TelegramLike.Shared.Api" />`; JwtBearer-пакет приходить транзитивно.
- **Realtime hub** — той самий JWT: `[Authorize]` на хабі, для WebSocket токен через `?access_token=` (`JwtBearerEvents.OnMessageReceived`, тільки на hub-шляху). **Gateway auth не робить** — форвардить `Authorization` як є; кожен сервіс валідує сам (gateway ≠ trust boundary).

**Config (appsettings + env):**
```
ServiceAuth__JwtSecret  = <base64 секрет> ⚠️ committed dev default — прийнятий ризик (див. кореневий CLAUDE.md §Auth)
ServiceAuth__Issuer     = "telegramlike-identity"
ServiceAuth__Audience   = "telegramlike-services"
```

**Рецепт для нового сервісу:** ProjectReference на `TelegramLike.Shared.Api` → `builder.Services.AddServiceJwtAuth(builder.Configuration)` → `app.UseAuthentication(); app.UseAuthorization();` → контролери успадковують `ApiControllerBase` (актор = `CurrentUserId`) → та сама `ServiceAuth__*` секція в appsettings + compose env. Публічні endpoint-и — `[AllowAnonymous]`.

**Threat model (що дизайн НЕ покриває):**
- Секрет симетричний (HMAC) — витік = можна підробити токен будь-якого `sub` для всіх сервісів. Для прода: secret store + rotation.
- Немає revocation — токен живе до `exp` навіть після logout; mitigation — короткий TTL (5 хв).

**Історія:** День 14 (2026-05-30) — перша версія: **Web був issuer-ом** (`iss=telegramlike-web`; `ServiceAuthOptions`/`ServiceTokenIssuer`/`ServiceAuthHandler` жили у Web) — закривала довіру до `X-User-Id` header між Web і Notifications. [TL-43..45] — Identity став IdP, issuer-файли з Web видалені. [TL-92] — валідаційний плюмбінг зведено у shared проєкт (правило «не шарити» стосується БД/домену, не інфраструктурного плюмбінгу).

Див. [[api-controllers]], [[microservices-migration]].
