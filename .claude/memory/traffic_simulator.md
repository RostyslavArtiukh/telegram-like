---
name: traffic-simulator
description: tools/TelegramLike.Simulator — консольна «вистава» з N ботів на Client SDK для спостереження у Grafana/RabbitMQ/Jaeger
metadata:
  type: project
---

# Симулятор трафіку — tools/TelegramLike.Simulator

Консольний застосунок: N ботів (дефолт 10) спілкуються годину через справжній стек, а користувач спостерігає у Grafana / RabbitMQ UI / Jaeger / Web UI. Свідомо **поза `TelegramLike.sln`** (як MAUI) — локальний інструмент, CI не збирає.

**Чому не тести:** годинна недетермінована симуляція — протилежність тесту; Testcontainers підняв би ізольовану інфраструктуру, і в моніторингу compose-стека нічого не було б видно.

**Архітектура:**
- Один бот = один DI-контейнер із `AddTelegramLikeClient` (бо SDK — «одна сесія на процес», singleton `TelegramLikeSession`); `BotClient` це обгортає.
- `BotActor` — цикл «пауза → зважена випадкова дія»: send/reply з typing (50%), реакція (20%), mark-as-read (15%), гортання історії (10%), retract (5%) + heartbeat 20с + SignalR-пуші хаба (чужі повідомлення боти бачать як справжні клієнти).
- `ChatTopology`: «Симуляція · Загальний» (усі) + broadcast «Оголошення» (пише лише власник) + 2 менші групи + дірект-пари по колу. Повторний запуск не дублює: групи шукаються за назвою, direct-create ідемпотентний.
- Боти: `sim_olena`…`sim_lev` (@sim.telegramlike.local), пароль у конфізі — **не міняти між запусками**.
- Інтенсивність конфігурована: `Simulator:BotCount/Duration/Min|MaxActionDelaySeconds` (appsettings/env/CLI). Стрес-пресет у README перевіряє алерти latency/5xx.

**Граблі, на які вже наступили:**
- NU1605: версії Microsoft.Extensions.* мають бути 9.0.10 (транзитивно через Http.Resilience 9.10.0), не 9.0.0.
- Після join'ів треба ~5с паузи: membership read-models у messaging/realtime fail-closed ([TL-101/103]) і наповнюються подіями асинхронно — інакше перші send'и ловлять 403.
- Free-юзер має ліміт 1 реакція на повідомлення → актор тримає в пам'яті `_reactedTo/_markedRead/_retracted`, інакше сиплються семантичні 400. Поодинокі 400 після рестарту (реакція на повідомлення, реаговане в минулому запуску через seed з історії) — очікувані.

Пов'язане: [[client_sdk_plan]], [[observability_metrics]], [[api_gateway]].
