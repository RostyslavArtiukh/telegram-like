---
name: observability-metrics
description: Prometheus metrics + Grafana dashboard across all apps (complements Jaeger tracing)
metadata: 
  node_type: memory
  type: project
  originSessionId: 4abaff75-364f-48ac-b7f4-bd4df8f811ef
---

**[TL-57/TL-58] (2026-07-04):** Metrics layer alongside the existing OTel tracing ([[observability-tracing]]).

**Instrumentation (TL-57):** all apps — 7 at the time (5 services + gateway + web); realtime joined later ([TL-77]) → **8 scrape targets now** — add `.WithMetrics(...)` next to `.WithTracing(...)` with AspNetCore + HttpClient + Runtime instrumentation + `AddPrometheusExporter()`, and `app.MapPrometheusScrapingEndpoint()` (public `/metrics`). Packages: `OpenTelemetry.Exporter.Prometheus.AspNetCore` **1.15.3-beta.1** (beta is its only channel), `OpenTelemetry.Instrumentation.Runtime` 1.15.1. Gateway also registers the `Yarp.ReverseProxy` meter; **web registers the `Polly` meter** so circuit-breaker/retry state is scrapeable. Metric names: `http_server_request_duration_seconds*` (RED), `http_client_request_duration_seconds*`, `dotnet_*` (gc/jit/threadpool/cpu/memory), `kestrel_*`.

**Prometheus + Grafana (TL-58):** in `monitoring/`. `prometheus.yml` = one scrape job per app (`<name>:8080/metrics`), 10s interval. Grafana auto-provisioned (`monitoring/grafana/provisioning/` datasource uid `prometheus` + dashboard provider) with `dashboards/telegramlike-overview.json` (6 panels: req rate, p95 latency, 5xx by service; BFF→downstream by status; gateway rate by route; working-set memory). Compose: `prometheus` :9090, `grafana` :3000 (anon Viewer on; admin/admin to edit).

**Verified live:** 8/8 targets up; p95 latency 7 series; dashboard + datasource provisioned.

**[TL-61] alerting:** `monitoring/rules.yml` — 3 alerts on confirmed metrics: `TargetDown` (`up==0` for 1m, critical), `HighHttp5xxRate` (>5% 5xx over 5m), `HighRequestLatencyP95` (p95>1.5s for 5m). `prometheus.yml` gains `rule_files` + `alerting.alertmanagers`. New `alertmanager` service (:9093), grouping config, **no external notifier** wired locally (add email/slack in `monitoring/alertmanager/alertmanager.yml` for real). Validate rules with `promtool` via `docker run --rm --entrypoint promtool prom/prometheus:v2.55.1 check rules /rules.yml` (needs `MSYS_NO_PATHCONV=1` on Git-Bash). Verified: stop a service → TargetDown pending→firing (~80s) → reaches Alertmanager `/api/v2/alerts`; recovery clears it. No Polly-breaker alert — that metric only emits on authed BFF traffic and its name wasn't confirmed; `up`/5xx cover "downstream bad".

**⚠️ Docker publish-layer cache gotcha (bit me here):** `docker compose --build` reused a cached .NET **publish** layer for the 5 pre-existing services and shipped **stale bits** (fresh image timestamp, but old code missing `/metrics`; no Prometheus DLL in `/app`). Fix: `docker compose build --no-cache <svc>`. Diagnose by checking `/app` for the expected DLL + the runtimeconfig.json timestamp. After changing service source, don't trust a plain `--build`.

**[TL-110] custom business + outbox metrics (2026-07-20):** second layer beyond RED — "is the product working", not just "are requests 200".
- **Shared outbox metrics** in `Infrastructure.ServiceDefaults/OutgoingEvents/`: `OutboxMetrics` (meter `TelegramLike.Outbox`) + `OutboxBacklogPoller` (10s, matches scrape interval) + `OutboxBacklog` record + `GetBacklogAsync` on the store + `OutgoingEventsIndexInitializer` (index `pending_by_age` on SentAt/DeadLetteredAt/OccurredAt — both the sender's claim and the poller's count scanned the whole collection before). All wired inside `AddOutgoingEvents`, so chats+messaging get it automatically. Series: `telegramlike_outbox_{published,publish_failures,dead_lettered}_total{event_type}`, `_pending`, `_dead_lettered_backlog`, `_oldest_pending_age_seconds`, `_publish_delay_seconds` (histogram).
- **Business counters:** `MessagingMetrics` (messages_sent{kind,broadcast}, reactions_added, messages_retracted{by_moderator}) and `ChatsMetrics` (chats_created{kind}, chat_membership_changes{change}), both in the Application layer, registered singleton in Program.cs. Counted **outside** `ConcurrencyRetry` — inside, a version-conflict retry double-counts.
- **Gotcha — custom meters need `.AddMeter(Name)`** in that service's `WithMetrics(...)`; an unlisted meter is silently dropped, no error.
- **Gotcha — default histogram buckets are milliseconds** (0, 5, 10, 25 … 10000). A seconds-valued histogram lands entirely in `le=5` and `histogram_quantile` returns garbage. Fix = a View: `.AddView("telegramlike.outbox.publish_delay", new ExplicitBucketHistogramConfiguration { Boundaries = OutboxMetrics.PublishDelayBucketsSeconds })` (0.1…60, straddling the 2s poll interval). Buckets can't be set on the instrument itself.
- **Gotcha — an OTel counter has no series until first increment.** A "No data" failures panel means "nothing ever failed", not "broken query".
- **⚠️ Verified by stopping RabbitMQ: `publish_failures_total` stayed flat while the queue grew to 254.** MassTransit blocks inside `Publish` waiting to reconnect instead of throwing, so the `catch` never runs. **The outage signal is the age gauge, not the error counter** — this is why `OutboxStalled` alerts on `oldest_pending_age_seconds > 60`. Full cycle verified live: stall → OutboxStalled fires (~70s) → broker back → drains to 0 → alert clears in ~10s.
- **RabbitMQ metrics:** `rabbitmq_prometheus` is enabled by default in `rabbitmq:3-management` on :15692 (published to the host too). Two scrape jobs: `/metrics` (aggregates) and `/metrics/detailed?family=queue_coarse_metrics` → `rabbitmq_detailed_queue_messages_ready{queue,vhost}` for per-queue depth. Restrict the family — asking for everything explodes series count.
- **Second dashboard** `telegramlike-business.json` ("TelegramLike — Product & Outbox", uid `telegramlike-business`), 12 panels, `$service` variable from `label_values(telegramlike_outbox_pending, job)` (so only outbox services appear). New alerts `OutboxStalled` + `OutboxDeadLettering`. New tests: `OutgoingEventsStoreBacklogTests` (5, in Chats.Tests — it owns the replica-set Mongo fixture).

See [[observability-tracing]], [[bff-resilience]], [[api-gateway]], [[integration-events-rabbitmq]], [[traffic-simulator]].
