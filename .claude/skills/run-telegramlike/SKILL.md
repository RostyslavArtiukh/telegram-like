---
name: run-telegramlike
description: Build, launch, and drive the TelegramLike stack. Use to run/start the app via docker compose, screenshot the Blazor Web UI, or confirm a change works end-to-end (register → login → create chat → send message → cross-user realtime) with the committed Playwright driver.
---

# Run TelegramLike

The whole app is one `docker compose` stack: MongoDB/Redis/RabbitMQ + 5 services +
realtime hub + YARP gateway + the **Blazor Server Web BFF at http://localhost:18080**
(Docker host ports = dev ports + 10000). There is no single-process "run" — you bring
up the stack, then drive the Web UI headlessly with the committed Playwright driver at
[.claude/skills/run-telegramlike/driver.mjs](driver.mjs).

Paths below are relative to the repo root. Docker must be running.

## Prerequisites
- Docker Desktop running (the stack builds ~8 service images; first build is slow,
  later builds reuse cached layers).
- Node 20+ and the Playwright npm package (browsers are already cached under
  `%LOCALAPPDATA%\ms-playwright`, so `npm i` only pulls the package, not browsers).

## Build + launch the stack
```bash
docker compose up -d --build
docker compose ps --format '{{.Service}} {{.Status}}'   # wait for web = (healthy)
curl -fsS http://localhost:18080/health/ready           # prints: Healthy
```
`web` is the last to go healthy (it depends on the gateway, which depends on all 5
services being healthy). Give it up to ~90s.

## Run (agent path — the driver)
```bash
cd .claude/skills/run-telegramlike
npm i
node driver.mjs
```
This registers Alice, logs her in, creates a group chat, sends a message, then opens a
**second** browser user (Bob) who joins by chat id, asserts he sees Alice's history,
replies, and asserts the reply reaches Alice's still-open page via the realtime push
(RabbitMQ → in-memory pubsub → Blazor circuit). Exit 0 = all checks passed. Screenshots
land in `.claude/skills/run-telegramlike/screenshots/` (`1-home-after-login`,
`2-alice-chat`, `3-bob-chat`, `4-alice-realtime` — read them to confirm).

Just want to poke the read+write path for one user (faster, no realtime assertion):
```bash
node driver.mjs --solo
```
Override the target with `BASE_URL=http://localhost:18080 node driver.mjs`.

The driver is the harness — extend it for the surface your change touches (it has small
`register`/`login`/`createGroup`/`joinChat`/`sendMessage`/`shot` helpers and a `retry`
wrapper). For the deeper assertion-focused flow and the MAUI/SDK client surfaces, see the
sibling `/verify` skill.

## Run (human path)
Open http://localhost:18080 in a browser, click **Register**, then **Sign in**. Useless
headless — that's what the driver is for.

## Gotchas (cost real debugging time)
- **Prerender trap.** Blazor Server renders a page before its SignalR circuit attaches;
  clicking too early does a static form post that silently no-ops. The driver waits ~2.5s
  after each navigation and wraps every interactive action in a 3× retry loop. Don't
  remove those waits.
- **Login is a native `<form method="post">`**, but register/create/send are interactive
  MudBlazor components — the two need different handling (login needs no circuit; the rest
  do). Selectors are MudBlazor, so select by **placeholder/label/role**, not by a
  `.form-control` bootstrap class (there isn't one).
- **Create group / Join navigate straight into `/chat/{id}`** — the driver reads the id
  from `page.url()`, not from a chat-list link.
- **Stale web image after a Web source change.** `docker compose build` can reuse a cached
  .NET publish layer and ship old bits (fresh image timestamp, old code). After editing
  Web source, rebuild that one image with no cache — `docker compose build --no-cache web`
  — then `docker compose up -d`. Same applies to any service you changed.
- **New project under `src/`?** The Web `Dockerfile` restore stage must `COPY` its csproj
  too, or the image build fails at restore.

## Troubleshooting
- `curl http://localhost:18080/health/ready` hangs or refuses → web isn't up yet;
  `docker compose ps` and wait for `(healthy)`, or `docker compose logs web`.
- Driver fails at register/login with a timeout → the circuit didn't attach in time;
  re-run (the retry loop usually absorbs it) or bump the `waitMs` in `driver.mjs`.
- `Cannot find package 'playwright'` → you skipped `npm i` in the skill dir (run it there,
  not at repo root — resolution is local to `.claude/skills/run-telegramlike/`).
