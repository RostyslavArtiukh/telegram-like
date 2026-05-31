---
name: TelegramLike pet project
description: Telegram-clone pet project as deliberate practice for DDD/NoSQL/clean code weak spots
type: project
originSessionId: 4698e4cf-aa88-44e8-a6f0-3047ea45edfb
---
**Що:** Pet-проект у стилі Telegram — чати 1-на-1, групові чати, реакції, presence.

**Стек (зафіксовано користувачем):** Blazor Server, MongoDB, Redis, RabbitMQ + MassTransit, мікросервіси, Docker.

**Why:** Це deliberate practice після поганого фідбеку зі співбесіди. Ціль — НЕ просто зробити Telegram, а прокачати DDD, NoSQL-моделювання та clean code на реальному (нетривіальному) домені. Чати — гарний домен для DDD: природні агрегати (Chat, Message), складна модель учасників, eventual consistency між сервісами.

**How to apply:**
- Архітектурні рішення вибирай так, щоб максимізувати навчальний вихід у слабких зонах. Наприклад: де є вибір між embedded і referenced документом — обговорюй компроміс, не вирішуй мовчки.
- Не пропонуй EF або SQL "для зручності". Стек зафіксовано.
- Перед написанням коду домовляйся про модель домену (мова, агрегати, інваріанти), а не одразу стрибай в Mongo-схему.
- Робочий каталог: d:\projects\Practice\TelegramLike (наразі порожній — стартуємо з нуля).

**Cadence (узгоджено 2026-05-04):** один етап плану на день, ~8 днів мінімум. Не стрибати наперед — кожен етап доводимо до кінця, фіксуємо артефакт, тоді наступний.

**Прогрес по плану:**
- 2026-05-04 (День 1): Етап 0 — Domain Discovery, працюємо з ubiquitous language. Артефакт: docs/domain.md (поки не створено, спочатку розмова).
