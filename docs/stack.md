# Tech Stack — TelegramLike

> ⚠️ **ЗАСТАРІЛО (2026-05).** Описує монолітну Clean-Architecture структуру (`Domain/Application/Infrastructure/Web` в одному процесі, Blazor Server як єдиний клієнт) — цього більше немає. Реальність: мікросервіси + YARP gateway + Blazor BFF + client SDK + MAUI + realtime SignalR. Джерело істини — кореневий `CLAUDE.md`.

> Зафіксовано: 2026-05-05.

---

## Платформа

| Шар | Технологія | Версія |
|---|---|---|
| Runtime | .NET | 9 |
| UI | Blazor Server | вбудований в ASP.NET Core 9 |
| База даних | MongoDB | 7 |
| Кеш / Real-time ephemeral | Redis | 7 |

---

## Архітектурний підхід

**Clean Architecture** з **DDD** та **CQRS (MediatR)**:

```
Web (Blazor Server)
  └── Application (Use Cases, Commands, Queries)
        └── Domain (Aggregates, Entities, VOs, Events)
  └── Infrastructure (MongoDB, Redis, BCrypt)
```

Залежності спрямовані тільки досередини: Web → Application → Domain. Infrastructure → Application (реалізує інтерфейси, описані в Application).

---

## Структура рішення

```
TelegramLike/
├── src/
│   ├── TelegramLike.Domain/          # Агрегати, Entity, VO, Domain Events, інтерфейси репозиторіїв
│   ├── TelegramLike.Application/     # Commands, Queries, Handlers, інтерфейси сервісів
│   ├── TelegramLike.Infrastructure/  # MongoDB репо, Redis, BCrypt, DI-реєстрація
│   └── TelegramLike.Web/             # Blazor Server: Pages, Components, SignalR broadcast
├── tests/
│   ├── TelegramLike.Domain.Tests/
│   ├── TelegramLike.Application.Tests/
│   └── TelegramLike.Infrastructure.Tests/
├── docker-compose.yml
└── TelegramLike.sln
```

---

## NuGet пакети

### TelegramLike.Domain
_(без зовнішніх залежностей — чистий домен)_

### TelegramLike.Application
| Пакет | Для чого |
|---|---|
| `MediatR` | CQRS: Commands, Queries, Domain Event handlers |
| `FluentValidation` | Валідація команд перед виконанням |

### TelegramLike.Infrastructure
| Пакет | Для чого |
|---|---|
| `MongoDB.Driver` | Офіційний .NET драйвер MongoDB |
| `StackExchange.Redis` | Redis: presence кеш, TypingIndicator pub/sub, сесії |
| `BCrypt.Net-Next` | Хешування паролів |

### TelegramLike.Web
| Пакет | Для чого |
|---|---|
| `MediatR` | Виклик команд/запитів зі Blazor компонентів |
| `FluentValidation.AspNetCore` | Інтеграція валідації з EditForm |

---

## Real-time у Blazor Server

Blazor Server вже працює через SignalR WebSocket (circuit). Для UI-оновлень при нових повідомленнях використовується:

```
IRealtimeNotifier          ← інтерфейс в Application
  └── RealtimeNotifier     ← реалізація в Infrastructure (Redis Pub/Sub)
        └── Blazor компоненти підписуються через IRealtimeNotifier.Subscribe(chatId, callback)
```

**Потік нового повідомлення:**
1. `SendMessage` команда → MongoDB зберігає
2. `MessageSent` domain event → `RealtimeNotifier.Publish(chatId, event)`
3. Redis pub/sub → всі сервери отримують подію
4. Blazor компоненти, що слухають `chatId`, викликають `StateHasChanged()`

Typing indicator: Redis `SETEX typing:{chatId}:{userId} 5 "1"` — без Blazor оновлення, через polling кожну секунду або окремий Redis subscription.

---

## Аутентифікація

- Cookie-based (Blazor Server — server-side, не SPA)
- `ASP.NET Core Authentication` middleware
- Власна реалізація (без ASP.NET Core Identity — занадто важка для pet project)
- Сесійний токен → Redis `session:{token}` → `userId`, TTL 7 днів

---

## Локальна розробка

```bash
docker-compose up -d    # MongoDB + Redis
dotnet run --project src/TelegramLike.Web
```

---

## TODO (День 3)

- [ ] Скаффолд solution + projects + references
- [ ] docker-compose.yml
- [ ] Базова конфігурація MongoDB + Redis у Web
- [ ] Структура папок Domain (по bounded contexts)
