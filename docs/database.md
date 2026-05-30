# Database Design — TelegramLike

> Складено: 2026-05-05. Оновлюється при кожній зміні схеми.

---

## СУБД

| Сховище | Роль |
|---|---|
| **MongoDB 7** | Основна база — всі 5 bounded contexts |
| **Redis 7** | Ephemeral дані: TypingIndicator (pub/sub + TTL), кеш OnlineStatus, сесійні токени |

**Чому MongoDB тут доречний:**
- `Message` з вкладеними `attachments` і `reactions` — природний документ
- `Notification.payload` — різна форма залежно від типу (flexible schema)
- `Chat.members` — вбудований список учасників для DirectChat (2 люди)
- Горизонтальне масштабування коробкою (`shardKey: chatId` для messages)

---

## Огляд колекцій

| Колекція | Bounded Context | Aggregate |
|---|---|---|
| `users` | Identity | `User` |
| `chats` | Chats | `Chat` |
| `chat_members` | Chats | `Member` |
| `user_chat_settings` | Chats | read model |
| `messages` | Messaging | `Message` |
| `hidden_messages` | Messaging | read model |
| `message_read_receipts` | Messaging | read model |
| `user_presence` | Presence | `UserPresence` |
| `notifications` | Notifications | `Notification` |
| `outbox` | Infrastructure (Messaging) | Transactional outbox для integration events |

---

## Колекції — документи та індекси

---

### `users`

```jsonc
{
  "_id": "uuid",                    // UserId
  "email": "user@example.com",     // unique index
  "hashedPassword": "$2b$...",
  "displayName": "Alice",
  "avatarUrl": null,
  "accountStatus": "active",       // active | deactivated | deleted
  "premiumExpiresAt": null,        // null = немає Premium
  "privacySettings": {
    "whoCanSeeLastSeen": "everyone", // everyone | contacts | nobody
    "whoCanSeePhone": "nobody",
    "whoCanAddToGroups": "everyone"
  },
  "blockedUserIds": ["uuid2", "uuid3"],  // вбудований список — зазвичай маленький
  "createdAt": "ISODate"
}
```

**Індекси:**
```js
{ email: 1 }                          // unique
{ accountStatus: 1 }                  // фільтр deleted акаунтів
{ "blockedUserIds": 1 }               // перевірка "чи заблокований"
```

**Примітки:**
- `blockedUserIds` — вбудований масив, а не окрема колекція. Типово < 100 записів, тому документ не розпухає.
- `hashedPassword` ніколи не повертається у відповідях API (projection: `{ hashedPassword: 0 }`).

---

### `chats`

```jsonc
{
  "_id": "uuid",
  "type": "direct",          // direct | group | broadcast
  "name": null,              // null тільки для direct
  "createdBy": "uuid",
  "createdAt": "ISODate",
  "deletedAt": null
}
```

**Індекси:**
```js
{ createdBy: 1 }
{ deletedAt: 1 }    // фільтр активних чатів
```

**Чому members — окрема колекція, а не вбудований масив:**
- `GroupChat` і `BroadcastChannel` можуть мати сотні учасників — вбудовування розпухне документ і зробить кожен запит важчим.
- `FormerMember` (`left | kicked | banned`) теж зберігається — масив росте.
- Окрема колекція дає незалежні індекси та запити по `status`, `role`.

---

### `chat_members`

```jsonc
{
  "_id": "uuid",
  "chatId": "uuid",
  "userId": "uuid",
  "role": "member",          // owner | admin | member | viewer
  "status": "active",        // active | left | kicked | banned
  "joinedAt": "ISODate",
  "leftAt": null,
  "kickedBy": null           // userId адміна
}
```

**Індекси:**
```js
{ chatId: 1, status: 1 }            // активні учасники чату
{ chatId: 1, userId: 1 }            // unique — один запис на людину в чаті
{ userId: 1, status: 1 }            // всі чати конкретного юзера
{ chatId: 1, role: 1 }              // список адмінів
```

---

### `user_chat_settings`

```jsonc
{
  "_id": "uuid",
  "userId": "uuid",
  "chatId": "uuid",
  "isArchived": false
}
```

**Індекси:**
```js
{ userId: 1, chatId: 1 }    // unique, основний запит
{ userId: 1, isArchived: 1 } // список архівованих
```

---

### `messages`

Центральна колекція. `attachments` і `reactions` — вбудовані, бо вони не живуть без повідомлення.

```jsonc
{
  "_id": "uuid",
  "chatId": "uuid",
  "authorId": "uuid",
  "text": "Привіт!",          // null якщо є attachments
  "attachments": [
    {
      "type": "image",        // image | file | audio | video
      "url": "https://...",
      "sizeBytes": 204800,
      "fileName": null
    }
  ],
  "replyToId": null,          // MessageId або null
  "forwardRef": null,         // { originalMessageId, originalChatId } або null
  "reactions": [
    {
      "memberId": "uuid",
      "emoji": "like",        // з фіксованого набору 8 emoji
      "addedAt": "ISODate"
    }
  ],
  "isRetracted": false,
  "retractedAt": null,
  "retractedBy": null,
  "broadcastReadCount": null, // integer тільки для BroadcastChannel, null для інших
  "sentAt": "ISODate"
}
```

**Індекси:**
```js
{ chatId: 1, sentAt: -1 }          // пагінація повідомлень (основний запит)
{ replyToId: 1 }                   // завантаження replies
{ authorId: 1, sentAt: -1 }        // повідомлення конкретного юзера
{ "reactions.memberId": 1 }        // перевірка реакцій юзера
{ chatId: 1, isRetracted: 1 }      // фільтр видалених
```

**Чому `reactions` вбудовані, а не окрема колекція:**
- Реакції — частина повідомлення, завжди завантажуються разом з ним.
- 8 emoji × N учасників — розмір масиву керований.
- Атомарний `$push` / `$pull` — без окремих транзакцій.

**Чому `broadcastReadCount` в документі:**
- `BroadcastChannel` не зберігає індивідуальні receipts (масштаб).
- `$inc` атомарний — race condition відсутній.
- Для `direct` і `group` — поле `null`, receipts в окремій колекції.

---

### `hidden_messages`

```jsonc
{
  "_id": "uuid",
  "messageId": "uuid",
  "userId": "uuid"
}
```

**Індекси:**
```js
{ userId: 1, messageId: 1 }    // unique — чи приховане повідомлення для мене
{ userId: 1 }                  // всі приховані для юзера (для фільтрації у списку)
```

---

### `message_read_receipts`

Тільки для `DirectChat` і `GroupChat`. BroadcastChannel використовує `broadcastReadCount` у документі `messages`.

```jsonc
{
  "_id": "uuid",
  "messageId": "uuid",
  "memberId": "uuid",
  "readAt": "ISODate"
}
```

**Індекси:**
```js
{ messageId: 1, memberId: 1 }   // unique — чи прочитав конкретний юзер
{ memberId: 1, readAt: -1 }     // останні прочитані юзером
```

---

### `user_presence`

```jsonc
{
  "_id": "uuid",          // userId (одна людина — один документ)
  "status": "offline",    // online | offline
  "lastSeenAt": "ISODate" // null якщо прихований налаштуваннями PrivacySettings
}
```

**Індекси:** первинний ключ = `_id`, додаткових не треба.

**Redis (поруч з MongoDB):**
```
presence:{userId}          → "online" | "offline"   TTL 30s  (heartbeat)
typing:{chatId}:{userId}   → "1"                    TTL 5s
session:{token}            → userId                 TTL 7d
```

`user_presence` в MongoDB — джерело істини для `lastSeenAt`. Redis — тільки кеш для real-time перевірок (щоб не бити в MongoDB на кожен heartbeat).

---

### `notifications`

```jsonc
{
  "_id": "uuid",
  "recipientId": "uuid",
  "type": "new_message",       // new_message | mention | member_joined | member_kicked
  "payload": {                 // flexible — форма залежить від type
    "chatId": "uuid",
    "messageId": "uuid",
    "actorId": "uuid"
  },
  "status": "pending",         // pending | delivered | read
  "createdAt": "ISODate",
  "readAt": null
}
```

**Індекси:**
```js
{ recipientId: 1, status: 1, createdAt: -1 }  // список непрочитаних
{ recipientId: 1, createdAt: -1 }             // вся стрічка сповіщень
```

**Чому `payload` — embedded object без фіксованої схеми:**
- `new_message` має `chatId + messageId + actorId`
- `member_joined` має лише `chatId + actorId`
- MongoDB дозволяє зберігати різну форму без nullable-колонок — це і є перевага над SQL тут.

---

## Діаграма зв'язків (концептуальна)

```
users ──────────────────────────────────────────────────────────┐
  │                                                              │
  │ blockedUserIds[]                                             │
  │                                                              │
  ├──► chat_members.userId ──► chats._id                        │
  │         │                                                    │
  │    role, status                                              │
  │                                                              │
  ├──► messages.authorId ──► messages._id                       │
  │         │                     │                             │
  │    attachments[]          reactions[]                        │
  │    (embedded)             (embedded)                         │
  │         │                                                    │
  │         └──► message_read_receipts.messageId                │
  │                                                              │
  ├──► user_presence._id (= userId)                             │
  │                                                              │
  └──► notifications.recipientId                                │
            │                                                    │
       payload.actorId ────────────────────────────────────────-┘
```

---

### `outbox` (День 9)

Transactional outbox для integration events — гарантує що подія потрапить у RabbitMQ навіть якщо брокер тимчасово недоступний.

```js
{
  _id: UUID,              // EventId
  EventType: String,      // assembly-qualified type name
  Payload: String,        // JSON-серіалізований integration event
  OccurredAt: Date,
  SentAt: Date | null,    // null = pending; не-null = опубліковано
  Retries: Int            // лічильник невдалих спроб
}
```

Індекси: за замовчуванням `_id`. Для `OutboxPublisher` запит `SentAt == null` сортується за `OccurredAt`. При зростанні розмірів додати:
- `{ SentAt: 1, OccurredAt: 1 }` — для polling-запиту publisher'а
- TTL index на `SentAt` (наприклад 7 днів) для прибирання опублікованих

Записується **у тій же Mongo-транзакції**, що й агрегат (через `IClientSessionHandle`), щоб гарантувати атомарність. Окремий `OutboxPublisherHostedService` дренує `SentAt == null` і публікує через MassTransit `IPublishEndpoint`.

---

## Стратегія міграцій

MongoDB не має схемних міграцій як SQL, але структура еволюціонує. Підхід:

- **Версіонування схеми**: поле `schemaVersion: 1` у документах де очікується еволюція
- **Lazy migration**: при читанні документа старої версії — трансформуємо на льоту в коді
- **Migration scripts**: для batch-оновлень — окремі скрипти в `db/migrations/`
- Інструмент: **migrate-mongo** або власні скрипти залежно від стеку

---

## TODO

- [ ] Вибрати ODM: **Mongoose** (Node.js) або нативний драйвер — після вибору стеку (День 3)
- [ ] Індекс `{ "reactions.memberId": 1, "reactions.emoji": 1 }` для перевірки ліміту Premium
- [ ] TTL index для `notifications` — автоматичне видалення старих сповіщень (наприклад 90 днів)
- [ ] Sharding strategy для `messages` при масштабуванні: `{ chatId: "hashed" }`
