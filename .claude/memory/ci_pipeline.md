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

**Green baseline:** 140 tests / 9 test projects на момент [TL-52]; після консолідації [TL-95..97] — **8 тест-проєктів, ~361 тест** (per-service, нейминг `Method_Scenario_ExpectedResult`).

**Known non-blocking annotations:** Node-20 deprecation on `actions/*` (auto-run on Node 24; wait for action updates); Testcontainers obsolete-ctor warnings — виправлено ([TL-95] прибрав останній у Notifications MongoFixture); build тепер 0 warnings.

**Другий job ([TL-77]):** `build-maui` на windows-latest (workload `maui-windows`, .NET 9+10) компілює `src/app/TelegramLike.App.csproj` під `net10.0-windows10.0.19041.0` — чистий compile-check без тестів (android TFM CI не провіжнить, тому не через .slnx). Деплой-кроків у CI нема. See [[microservices-migration]].
