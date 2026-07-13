---
name: api_controllers
description: All 5 services expose HTTP via classic [ApiController] controllers (not minimal API) — shared pieces, the split rule, and the per-service wire-contract rule
metadata:
  type: project
---

All 5 services (chats, identity, notifications, presence, messaging) expose their HTTP API via **classic ASP.NET Core controllers, not minimal API**. Migrated `[TL-46]`..`[TL-50]` (was minimal `MapGroup`/`MapPost`/`MapGet` in `Program.cs`). Routes, verbs, status codes, error bodies, and enum serialization were preserved byte-for-byte — the Web BFF typed clients were untouched.

Each `*.Api` project has:
- `ApiControllerBase` — `[ApiController]` base; `TryGetUserId(out)` / `CurrentUserId` resolve the actor from JWT `sub` (fallback `ClaimTypes.NameIdentifier`); relies on `MapInboundClaims=false`. Was copied verbatim per service; **since [TL-92] it lives once in `src/shared/TelegramLike.Api.ServiceDefaults`** (together with `AddServiceJwtAuth`; skips `[AllowAnonymous]` endpoints so Identity's public auth endpoints keep working). `DomainExceptionFilter` stays per-service on purpose (next bullet).
- `Controllers/*.cs` — **thin** controllers (logic stays in MediatR handlers), split by responsibility and grouped **by the resource being mutated**, not by whether an actor/permission check exists (e.g. chat `rename` is actor-authorized but lives with chat lifecycle in `ChatsController`, not membership). Split per service: chats → Chats/ChatMembers · identity → Auth(anon)/Users(authed) · notifications → Feed/Read · presence → Presence/Typing · messaging → Messages/Reactions/ReadReceipts.
- `Filters/DomainExceptionFilter.cs` — global `IExceptionFilter`, registered via `AddControllers(o => o.Filters.Add<DomainExceptionFilter>())`. **Reproduces each service's pre-existing wire contract — never a blanket copy of the chats version:**
  - chats / messaging: `InvalidOperationException`+`ArgumentException`→400, `UnauthorizedAccessException`→403, `ProblemDetails` body.
  - identity: `ValidationException`+`InvalidOperationException`→400, body is `{ error }` (the Web BFF Identity client reads `error`), **not** `ProblemDetails`.
  - notifications: only `InvalidOperationException`→400 `ProblemDetails`.
  - presence: **no-op** — the old API caught nothing, so every handler exception was a 500.
- `Contracts/` — request/response `public sealed record`s (where any exist; presence has none — only a bare `Guid[]` body).
- `Program.cs` — `AddControllers(...)` + `MapControllers()`. `.AddJsonOptions(JsonStringEnumConverter)` **only** where the service already registered it (chats, messaging — load-bearing for `MemberRole` / `Emoji` / `AttachmentType`); identity/notifications/presence keep enums numeric. Health endpoints stay minimal (`MapHealthChecks` / `MapGet("/health")`). Auth/OTel/health/DI/MassTransit/outbox wiring is unchanged from the minimal-API era.

**Adding an endpoint:** add the action to the right responsibility controller (or a new controller if it's a new resource), keep it thin (→ MediatR), reuse `ApiControllerBase` for the actor, let `DomainExceptionFilter` map errors. Routes / verbs / status codes / error-body shape / enum-serialization are a contract with the Web BFF typed clients — don't change them without updating the corresponding client. See [[microservices_migration]].

**Web BFF too — no inline endpoints in `Program.cs`.** The Blazor host's `/auth` callbacks (`signin`, `signout` — the native-`<form>` POST targets from `Login.razor` / `NavMenu.razor`) live in `src/TelegramLike.Web/Controllers/AuthController.cs` ([TL-90]), registered via `AddControllers()` + `MapControllers()`. NOT an `[ApiController]` / `ApiControllerBase` / `DomainExceptionFilter` case — these are redirect-returning cookie callbacks, not JSON, so it's a plain `ControllerBase` that validates antiforgery manually (`[IgnoreAntiforgeryToken]` + `IAntiforgery.ValidateRequestAsync`) with actions named `LogIn`/`LogOut` (renamed off the base `SignIn`/`SignOut` members to avoid the hide warning). User preference: **never leave `MapPost`/`MapGet` bodies inline in `Program.cs`** — endpoints belong in a controller. See [[readable_naming_and_mudblazor]].
