# Memory Index — TelegramLike

- [project_status.md](project_status.md) — Поточний стан та план розробки TelegramLike
- [chats_persistence.md](chats_persistence.md) — Дві колекції + MongoDB транзакція для агрегату Chat
- [notifications_fanout.md](notifications_fanout.md) — Cross-context fanout: тепер асинхронно через RabbitMQ (MessageSentIntegrationEvent → consumer → FanoutChatNotificationCommand)
- [integration_events_rabbitmq.md](integration_events_rabbitmq.md) — RabbitMQ + MassTransit + Transactional Outbox; як додавати нові integration events
- [microservices_migration.md](microservices_migration.md) — Інкрементальна міграція з monolith у мікросервіси; прогрес і архітектурні рішення
- [service_auth_jwt.md](service_auth_jwt.md) — JWT auth між Web BFF і downstream сервісами; рецепт додавання auth до нового сервісу
- [realtime_blazor_pubsub.md](realtime_blazor_pubsub.md) — Real-time UI через RabbitMQ → in-memory pubsub → Blazor circuit (без окремого SignalR Hub)
- [observability_tracing.md](observability_tracing.md) — OpenTelemetry → Jaeger; як додавати spans і пропагація через HTTP+RabbitMQ
- [testing_setup.md](testing_setup.md) — xUnit + FluentAssertions + NSubstitute + Testcontainers; directConnection fix для Mongo
- [permissions_preference.md](permissions_preference.md) — Юзер не хоче постійних prompts; широкий allowlist у settings.local.json
- [nomenclature_step_not_day.md](nomenclature_step_not_day.md) — Маркувати нові ітерації як "Step N" а не "Day N" (продовжувати нумерацію після Day 21)
- [memory_dual_write.md](memory_dual_write.md) — Дзеркалити memory у `.claude/memory/` репо для синхронізації між машинами
- [user_profile.md](user_profile.md) — Профіль користувача та стиль роботи
