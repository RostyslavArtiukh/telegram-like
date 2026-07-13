---
name: observability-tracing
description: OpenTelemetry tracing на всіх 8 застосунках з експортом у Jaeger; як додавати нові spans і нові інструментації
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

Step 27 (2026-05-31): додано distributed tracing.

> **Оновлення 2026-07-13:** трейсинг емітять усі **8 застосунків** — web, gateway, 5 сервісів, realtime (`telegramlike.<name>`); gateway+realtime додатково вичищають `?access_token=` зі спанів (`RedactAccessTokenProcessor`). Згадки «3 сервіси» нижче — стан на Step 27.

**Why:** до цього не було способу побачити end-to-end flow Web → Notifications/Presence. Логи окремі по сервісам, кореляція ручна по timestamp. Тепер один traceId зв'язує всі spans.

**How to apply:** коли треба додати spans на новий сервіс — copy-paste `AddOpenTelemetry` блок з [Program.cs](src/TelegramLike.Web/Program.cs) (~ 15 рядків), поміняти `service.name`. Нові custom spans — `using var activity = ActivitySource.StartActivity("name")`; ActivitySource має бути зареєстрований через `.AddSource("name")`.

**Стек:**
- SDK: `OpenTelemetry.Extensions.Hosting` 1.15.x
- Інструментація: `OpenTelemetry.Instrumentation.AspNetCore` (incoming HTTP) + `OpenTelemetry.Instrumentation.Http` (outgoing HttpClient).
- Exporter: `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP/gRPC) → Jaeger
- Backend: `jaegertracing/all-in-one:1.60` (UI на :16686, OTLP receiver на :4317). `COLLECTOR_OTLP_ENABLED=true` env.

**Що покрито автоматично:**
- HTTP server: всі `/notifications/*`, `/presence/*`, Web pages — span per request з `http.*`, `url.path`, `http.response.status_code`.
- HTTP client (тільки у Web): outgoing виклики до Notifications/Presence через `INotificationsApi`/`IPresenceApi`.
- **MassTransit (через `.AddSource("MassTransit")`):** publish + consume автоматично. Trace context (traceparent) injects у RabbitMQ message headers, на consumer side extracts → один traceId через RabbitMQ boundary.

**Що НЕ покрито (поки):**
- MongoDB driver — треба `MongoDB.Driver.Core.Extensions.DiagnosticSources` package.
- StackExchange.Redis — треба `OpenTelemetry.Instrumentation.StackExchangeRedis`.
- Outbox-publisher loop (`OutgoingEventsSender`) — створює свій `ActivitySource` не зареєстровано. Якщо хочеш бачити "outbox.publish" окремим span — додай `private static readonly ActivitySource Source = new("TelegramLike.Outbox"); using var a = Source.StartActivity("publish")` і `.AddSource("TelegramLike.Outbox")` у Web Program.cs. Span MassTransit publish все одно з'явиться як дочірній.

**Trace context propagation:**
- HTTP: автоматично через `traceparent` header (W3C Trace Context).
- RabbitMQ: MassTransit сам сериалізує traceparent у message headers. Перевірено у Jaeger.

**Конфіг:**
- `Tracing:OtlpEndpoint` — якщо порожнє, exporter не реєструється (silent no-op для local `dotnet run`).
- У docker — `http://jaeger:4317`.

**Sampling:** наразі always-on (100%). Для prod треба `SetSampler(new TraceIdRatioBasedSampler(0.1))`.

**Як подивитись traces:**
1. `docker compose up -d` (jaeger разом стартує)
2. `http://localhost:16686` → Service dropdown → pick `telegramlike.web` (або будь-який з 8: `.gateway`/`.identity`/`.chats`/`.messaging`/`.notifications`/`.presence`/`.realtime`)
3. "Find Traces" → клік на trace → видно span tree через сервіси.

**Перевірка через API (без UI):**
- `curl http://localhost:16686/api/services` → список сервісів.
- `curl "http://localhost:16686/api/traces?service=telegramlike.web&limit=10"` → traces.
- `curl http://localhost:16686/api/services/telegramlike.notifications/operations` → endpoint-list.
