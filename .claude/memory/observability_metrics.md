---
name: observability-metrics
description: Prometheus metrics + Grafana dashboard across all apps (complements Jaeger tracing)
metadata:
  node_type: memory
  type: project
---

**[TL-57/TL-58] (2026-07-04):** Metrics layer alongside the existing OTel tracing ([[observability-tracing]]).

**Instrumentation (TL-57):** all 7 apps (5 services + gateway + web) add `.WithMetrics(...)` next to `.WithTracing(...)` with AspNetCore + HttpClient + Runtime instrumentation + `AddPrometheusExporter()`, and `app.MapPrometheusScrapingEndpoint()` (public `/metrics`). Packages: `OpenTelemetry.Exporter.Prometheus.AspNetCore` **1.15.3-beta.1** (beta is its only channel), `OpenTelemetry.Instrumentation.Runtime` 1.15.1. Gateway also registers the `Yarp.ReverseProxy` meter; **web registers the `Polly` meter** so circuit-breaker/retry state is scrapeable. Metric names: `http_server_request_duration_seconds*` (RED), `http_client_request_duration_seconds*`, `dotnet_*` (gc/jit/threadpool/cpu/memory), `kestrel_*`.

**Prometheus + Grafana (TL-58):** in `monitoring/`. `prometheus.yml` = one scrape job per app (`<name>:8080/metrics`), 10s interval. Grafana auto-provisioned (`monitoring/grafana/provisioning/` datasource uid `prometheus` + dashboard provider) with `dashboards/telegramlike-overview.json` (6 panels: req rate, p95 latency, 5xx by service; BFF→downstream by status; gateway rate by route; working-set memory). Compose: `prometheus` :9090, `grafana` :3000 (anon Viewer on; admin/admin to edit).

**Verified live:** 8/8 targets up; p95 latency 7 series; dashboard + datasource provisioned.

**⚠️ Docker publish-layer cache gotcha (bit me here):** `docker compose --build` reused a cached .NET **publish** layer for the 5 pre-existing services and shipped **stale bits** (fresh image timestamp, but old code missing `/metrics`; no Prometheus DLL in `/app`). Fix: `docker compose build --no-cache <svc>`. Diagnose by checking `/app` for the expected DLL + the runtimeconfig.json timestamp. After changing service source, don't trust a plain `--build`.

See [[observability-tracing]], [[bff-resilience]], [[api-gateway]].
