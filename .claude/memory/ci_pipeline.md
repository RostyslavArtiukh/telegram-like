---
name: ci-pipeline
description: GitHub Actions CI — build+test the whole solution on push/PR to master
metadata:
  node_type: memory
  type: project
---

**[TL-52] (2026-07-04):** First CI on the repo. `.github/workflows/ci.yml` — one `build-test` job on `ubuntu-latest`: restore → build (Release) → `dotnet test` the full `TelegramLike.sln`, uploads `.trx` as an artifact. Triggers on push + PR to `master`; concurrency-capped per ref.

**Why ubuntu-latest:** it ships Docker, which the Testcontainers-based Infrastructure/Api integration tests require (Mongo/Redis containers). Whole run ≈ 1m29s.

**Pins .NET 9 SDK** via `actions/setup-dotnet` (`9.0.x`) — projects are `net9.0`, no `global.json`, and the local dev box has SDK 10, so CI pins 9 for determinism.

**Green baseline:** 140 tests / 9 test projects.

**Known non-blocking annotations:** Node-20 deprecation on `actions/*` (auto-run on Node 24; wait for action updates); Testcontainers obsolete-ctor warnings (`MongoDbBuilder()`/`RedisBuilder()` in Presence.Infrastructure.Tests fixtures) — trivial fix, not yet done.

**Next planned (this track):** optional fast/Docker job split (TL-53, low value while run is ~90s), then BFF resilience via `Microsoft.Extensions.Http.Resilience` (TL-54). See [[microservices-migration]].
