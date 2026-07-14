# TelegramLike.Simulator — вистава для моніторингу 🎭

Консольний симулятор живого трафіку: N ботів (за замовчуванням 10) годину
спілкуються між собою через **справжній стек** — gateway → сервіси → RabbitMQ →
realtime-хаб, — а ти спостерігаєш у Grafana, RabbitMQ UI, Jaeger і в самому Web UI.

Кожен бот — повноцінний клієнт на `TelegramLike.Client` (той самий SDK, що й
MAUI-застосунок): логін через Identity, HTTP через gateway з resilience-пайплайном,
SignalR-підключення до realtime-хаба, presence heartbeat кожні 20с.

## Запуск

```bash
# 1. Підняти стек (якщо ще не працює)
docker compose up -d --build

# 2. Запустити виставу (з кореня репозиторію)
dotnet run --project tools/TelegramLike.Simulator
```

Зупинка — `Ctrl+C` (боти коректно йдуть офлайн) або сама завершиться після `Duration`.

Проєкт свідомо **не входить** у `TelegramLike.sln` — це локальний інструмент, CI його не збирає.

## Що роблять боти

Кожен актор у циклі «випадкова пауза → випадкова дія»:

| Дія | Вага | Що навантажує |
|---|---|---|
| ✉ написати / ↩ відповісти (з typing-індикатором) | 50% | messaging + outbox, notifications fanout, presence typing, realtime push |
| ❤ реакція на чуже повідомлення | 20% | messaging (optimistic concurrency!), realtime push |
| 👁 позначити прочитаним | 15% | messaging, notifications `UnreadCountChanged` |
| 📜 погортати історію чату | 10% | read-шлях messaging |
| 🗑 відкликати своє повідомлення | 5% | messaging retract flow |

Чужі повідомлення боти бачать через SignalR-пуші хаба — як справжні клієнти.

**Сцена:** група «Симуляція · Загальний» (усі), broadcast «Симуляція · Оголошення»
(пише лише власник `sim_olena`), дві менші групи («Робота» / «Дозвілля» — половини
трупи) та дірект-діалоги по колу. Повторний запуск чати не дублює: групи
знаходяться за назвою, дірект-create ідемпотентний.

## Інтенсивність (конфіг)

`appsettings.json`, секція `Simulator`; будь-що можна перебити з CLI:

```bash
# Жвавіша розмова: паузи 2–8 с
dotnet run --project tools/TelegramLike.Simulator -- --Simulator:MinActionDelaySeconds=2 --Simulator:MaxActionDelaySeconds=8

# Коротка репетиція на 10 хвилин
dotnet run --project tools/TelegramLike.Simulator -- --Simulator:Duration=00:10:00

# Стрес: 30 ботів, паузи 0.5–2 с — подивись, чи спрацюють алерти latency/5xx
dotnet run --project tools/TelegramLike.Simulator -- --Simulator:BotCount=30 --Simulator:MinActionDelaySeconds=0.5 --Simulator:MaxActionDelaySeconds=2
```

| Параметр | Дефолт | Значення |
|---|---|---|
| `GatewayBaseUrl` | `http://localhost:18090` | gateway docker-стека (для `dotnet run`-стека — `:8090`) |
| `BotCount` | `10` | розмір трупи (перші 10 — з іменами, далі масовка) |
| `Duration` | `01:00:00` | тривалість вистави |
| `MinActionDelaySeconds` / `MaxActionDelaySeconds` | `4` / `20` | пауза між діями одного бота |
| `Password` | `SimBots-2026!` | один на всіх ботів; **не міняй між запусками** — акаунти вже в Identity |

## Куди дивитись

- **Web UI** — http://localhost:18080. Увійди будь-яким ботом
  (`sim_olena@sim.telegramlike.local` / пароль із конфіга) і дивись розмову наживо —
  повідомлення, typing, presence, unread-бейджі оновлюються в реальному часі.
- **Grafana** — http://localhost:3000: RPS та latency p95 по всіх 8 застосунках,
  5xx. На стрес-пресеті перевір алерти `HighRequestLatencyP95` / `HighHttp5xxRate`
  (Alertmanager: http://localhost:9093).
- **RabbitMQ UI** — http://localhost:15672 (vhost `telegramlike`): message rates
  по exchange/чергах — видно fanout нотифікацій, outbox-потік chats/messaging,
  сигнальні події presence.
- **Jaeger** — http://localhost:16686: наскрізні трейси
  gateway → messaging → RabbitMQ → notifications/realtime.
- **Консоль симулятора** — кольоровий лог дій кожного бота + статистика щохвилини.

## Ліміти за годину (дефолтна інтенсивність)

10 ботів × дія кожні ~12 с ≈ 3000 дій: ~1500 повідомлень, ~600 реакцій,
~450 прочитань — плюс fanout кожної події на всіх учасників чату. Достатньо,
щоб графіки жили, і замало, щоб щось перегріти.
