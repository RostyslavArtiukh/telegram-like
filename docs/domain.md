# Domain Model — TelegramLike

> Ubiquitous Language: терміни зафіксовані 2026-05-04.
> Усі назви — англійською, бо вони йдуть у код. Пояснення — українською.
> Документ оновлюється при кожній зміні домену.

---

## Bounded Contexts (попередній поділ)

| Context | Відповідальність |
|---|---|
| **Identity** | Реєстрація, вхід, профіль, блокування |
| **Chats** | Створення чатів, учасники, ролі |
| **Messaging** | Повідомлення, реакції, read receipts |
| **Presence** | Online-статус, typing-індикатор |
| **Notifications** | Непрочитані, push-повідомлення |

> Один і той самий "User" у різних контекстах виглядає по-різному. В Identity він `User` з паролем і email. У Chats він `Member` з роллю. Це різні концепти — не одна сутність, яку тягнемо скрізь.

---

## Glossary — Actors

### `User`
Людина, зареєстрована в системі. Живе в контексті **Identity**.
Має: email, пароль, ім'я, аватар, налаштування конфіденційності.

### `Member`
Той самий `User`, але *всередині конкретного чату*. Живе в контексті **Chats**.
Має: роль, дату приєднання, статус (`Active | Left | Kicked | Banned`).
- **Не плутати з `User`** — це різні агрегати в різних контекстах.

### `FormerMember`
Стан `Member` після виходу (`Left`) або виключення (`Kicked`).
Зберігається для адмін-аудиту: хто колись був у групі.
Фіксується подією `MemberLeft` (сам пішов) або `MemberKicked` (адмін вигнав).
- **Важливо:** FormerMember НЕ може писати в чат, але його минулі повідомлення залишаються.

---

## Glossary — Chat Types

Батьківський концепт — **`Chat`** (не "Channel" — щоб уникнути колізії з `BroadcastChannel`).

### `DirectChat`
Чат між рівно двома `User`-ами. Симетричний — обидва можуть писати.
- Якщо один `User` **блокує** іншого — чат лишається, але заблокований не може надсилати повідомлення і не може знайти того, хто заблокував.
- Якщо `User` **видаляє акаунт** — чат лишається, але надіслати повідомлення вже неможливо.

### `GroupChat`
Чат для N учасників. Усі `Member` з роллю `Member` і вище можуть писати.

### `BroadcastChannel`
Чат для N учасників, де писати можуть лише `Admin` і `Owner`. Решта — читають і реагують.
Відрізняється від `GroupChat` саме цим інваріантом, не кількістю людей.

---

## Glossary — Roles (у межах Chat)

Ролі існують лише всередині `Chat`. Поза чатом є лише `User`.

| Роль | Хто | Що може |
|---|---|---|
| `Owner` | Творець чату | Все. Єдиний хто може видалити `GroupChat` / `BroadcastChannel`. Передати ownership. |
| `Admin` | Призначається `Owner` | Керувати учасниками (kick, ban), змінювати налаштування чату, видаляти чужі повідомлення (`RetractMessage`). |
| `Member` | Звичайний учасник | Писати повідомлення, реагувати, виходити. |
| `Viewer` | Read-only учасник | Читати, реагувати. Не може надсилати повідомлення. Використовується в `BroadcastChannel` для всіх неадмінів. |

> `Viewer` — це **роль**, не стан відвідувача який ще не приєднався. Людина "поза чатом" — просто `User`, без ролі.

---

## Glossary — Messages

### `Message`
Основна одиниця спілкування. Надсилається `Member`-ом у `Chat`.

### `Reply`
Повідомлення (`Message`) з опціональним посиланням на інше повідомлення: `replyToId: MessageId`.
Це **не окрема сутність** — це той самий `Message` з заповненим `replyToId`.
Інваріант: не можна відповісти на повідомлення, яке було `Retracted`.

### `HideMessage`
**Команда.** Прибирає повідомлення з мого перегляду — тільки для мене.
Інші `Member` повідомлення продовжують бачити.
Реалізується як запис `HiddenMessage { MessageId, UserId }` — не змінює сам `Message`.

### `RetractMessage`
**Команда.** Відкликає повідомлення для всіх.
Хто може: автор повідомлення, `Admin`, `Owner`.
Реалізується як soft-delete з прапорцем `IsRetracted = true`. Текст видаляється, але запис лишається (для аудиту і щоб `Reply` не "зламались").

---

## Glossary — Reactions

### `Reaction`
Емоція `Member`-а на конкретне `Message`. Визначається emoji.
- Один `Member` — **максимум 1 реакція** на одне повідомлення (або 2 для Premium-акаунту).
- Emoji set: фіксований (визначити список — TODO).
- Інваріант: не можна реагувати на `Retracted` повідомлення.

---

## Glossary — Presence

### `OnlineStatus`
Стан активності `User`-а. Видимість контролюється налаштуваннями конфіденційності.

| Стан | Значення |
|---|---|
| `Online` | Активний зараз |
| `Offline` | Неактивний |
| `LastSeen` | Часовий штамп останньої активності |

### `TypingIndicator`
Ефемерний сигнал: `Member` у конкретному `Chat` зараз друкує.
Не зберігається — тільки real-time через Redis.

### `MessageReadReceipt`
Факт прочитання `Message` конкретним `Member`-ом.
Сутність: `{ MessageId, MemberId, ReadAt }`.
У `BroadcastChannel` — лише **лічильник** прочитань, не індивідуальні записи (масштаб).

---

## Glossary — Архівування (локальна операція)

### `ArchiveChat`
**Команда.** `User` приховує `Chat` зі свого основного списку.
Чат лишається **повністю функціональним** — нові повідомлення приходять.
Це персональне налаштування, а не стан `Chat`-у.
Реалізується як запис `UserChatSettings { UserId, ChatId, IsArchived }` — не змінює агрегат `Chat`.

---

## Glossary — Блокування

### `BlockUser`
**Команда.** `User` блокує іншого `User`-а (контекст Identity, не Chats).
Наслідки для `DirectChat`:
- Заблокований не може надсилати повідомлення.
- Заблокований не може знайти того, хто заблокував.
- `DirectChat` лишається в обох — повідомлення, що були, не видаляються.

---

## Commands (наміри — що хочемо зробити)

| Команда | Хто може | Контекст |
|---|---|---|
| `SendMessage` | `Member` (не `Viewer`, не `FormerMember`) | Messaging |
| `RetractMessage` | Автор, `Admin`, `Owner` | Messaging |
| `HideMessage` | Будь-який `User` | Messaging (локально) |
| `AddReaction` | `Member`, `Viewer` | Messaging |
| `RemoveReaction` | Автор реакції | Messaging |
| `JoinChat` | `User` | Chats |
| `LeaveChat` | `Member` | Chats |
| `KickMember` | `Admin`, `Owner` | Chats |
| `RenameChat` | `Admin`, `Owner` | Chats |
| `ArchiveChat` | `User` | Chats (локально) |
| `BlockUser` | `User` | Identity |

---

## Domain Events (факти — що сталося)

| Подія | Виникає коли |
|---|---|
| `MessageSent` | Успішно виконано `SendMessage` |
| `MessageRetracted` | Успішно виконано `RetractMessage` |
| `ReactionAdded` | Успішно виконано `AddReaction` |
| `ReactionRemoved` | Успішно виконано `RemoveReaction` |
| `MemberJoined` | Виконано `JoinChat` |
| `MemberLeft` | `Member` сам виконав `LeaveChat` |
| `MemberKicked` | Адмін виконав `KickMember` |
| `ChatCreated` | Новий `Chat` будь-якого типу створено |
| `UserBlocked` | Виконано `BlockUser` |

---

## Key Invariants (бізнес-правила, які ніколи не порушуються)

1. `Member` зі статусом `Left`, `Kicked`, або `Banned` не може виконати `SendMessage`.
2. `Viewer` не може виконати `SendMessage` (тільки `AddReaction`).
3. `RetractMessage` — тільки автор, `Admin`, або `Owner`. Ніхто інший.
4. Не можна `AddReaction` або `Reply` до `Retracted` повідомлення.
5. `Owner` — єдиний хто може видалити `GroupChat` або `BroadcastChannel`.
6. У `BroadcastChannel` `SendMessage` дозволено лише `Admin` і `Owner`.
7. Один `Member` — максимум 1 реакція на `Message` (2 для Premium).
8. `TypingIndicator` ніколи не персистується — тільки real-time.
9. `ArchiveChat` і `HideMessage` — локальні операції, не змінюють стан агрегатів `Chat` / `Message`.

---

---

## Aggregate Design

> Для кожного Bounded Context — Aggregate Root, Entities всередині нього, Value Objects (immutable, порівнюються за значенням), та які Domain Events він публікує.

---

### Context: Identity

#### Aggregate: `User`
**Aggregate Root:** `User`

| Тип | Назва | Опис |
|---|---|---|
| Value Object | `UserId` | UUID, незмінний після створення |
| Value Object | `Email` | Валідований формат, унікальний в системі |
| Value Object | `HashedPassword` | bcrypt-хеш, ніколи не повертається назовні |
| Value Object | `DisplayName` | Відображуване ім'я, 1–64 символи |
| Value Object | `Avatar` | URL до файлу або `null` |
| Value Object | `PrivacySettings` | `{ whoCanSeePhone, whoCanSeeLastSeen, whoCanAddToGroups }` — enum-поля |
| Value Object | `AccountStatus` | `Active \| Deactivated \| Deleted` |
| Value Object | `PremiumStatus` | `None \| Active { expiresAt }` — живе в Identity, не окремий контекст |

**Entity всередині агрегату:** `BlockEntry { blockedUserId: UserId, blockedAt: DateTime }`
— список заблокованих живе всередині `User`, оскільки блокування — персональне налаштування.

**Domain Events:**
- `UserRegistered { userId, email, displayName, registeredAt }`
- `UserBlocked { blockerId, blockedUserId, at }`
- `UserUnblocked { blockerId, blockedUserId, at }`
- `ProfileUpdated { userId, changedFields }`
- `AccountDeactivated { userId, at }`

**Інваріанти:**
- `Email` унікальний — перевіряється на рівні репозиторію перед збереженням.
- `BlockEntry` не може містити власний `UserId` (не можна заблокувати себе).
- `Deleted` акаунт незворотній — `AccountStatus` не переходить назад в `Active`.

---

### Context: Chats

#### Aggregate: `Chat`
**Aggregate Root:** `Chat` (sealed hierarchy: `DirectChat | GroupChat | BroadcastChannel`)

| Тип | Назва | Опис |
|---|---|---|
| Value Object | `ChatId` | UUID |
| Value Object | `ChatType` | `Direct \| Group \| Broadcast` |
| Value Object | `ChatName` | 1–128 символів; відсутній у `DirectChat` |
| Entity | `Member` | `{ memberId, userId, role, status, joinedAt }` |
| Value Object | `MemberRole` | `Owner \| Admin \| Member \| Viewer` |
| Value Object | `MemberStatus` | `Active \| Left \| Kicked \| Banned` |

**Entity `Member`** — єдина сутність всередині агрегату `Chat`. `FormerMember` — це не окремий агрегат, а `Member` зі статусом `Left | Kicked | Banned`. Зберігається для аудиту.

**Рішення по `BannedMember`:** `Banned` — стан у `MemberStatus`. Окремий агрегат `Ban` не потрібен на поточному етапі. Якщо знадобиться логіка "бан на час" — додамо `BanEntry { reason, bannedAt, bannedBy, expiresAt? }` як Value Object всередині `Member`.

**Domain Events:**
- `ChatCreated { chatId, type, createdBy, at }`
- `MemberJoined { chatId, userId, role, at }`
- `MemberLeft { chatId, userId, at }`
- `MemberKicked { chatId, userId, kickedBy, at }`
- `MemberBanned { chatId, userId, bannedBy, reason, at }`
- `MemberRoleChanged { chatId, userId, oldRole, newRole, changedBy, at }`
- `ChatRenamed { chatId, oldName, newName, renamedBy, at }`
- `ChatDeleted { chatId, deletedBy, at }`

**Інваріанти:**
- `DirectChat` має рівно 2 `Member`-и і не має `ChatName`.
- `Owner` — рівно один у кожному `GroupChat` / `BroadcastChannel`.
- `Owner` не може залишити чат без передачі ownership (`TransferOwnership` → потім `LeaveChat`).
- `BroadcastChannel`: новий `Member` через `JoinChat` отримує роль `Viewer` автоматично.

#### Read Model: `UserChatSettings`
Не агрегат — персональне налаштування. Зберігається окремо.
`{ userId: UserId, chatId: ChatId, isArchived: bool }`
Не публікує подій — лише CRUD.

---

### Context: Messaging

#### Aggregate: `Message`
**Aggregate Root:** `Message`

| Тип | Назва | Опис |
|---|---|---|
| Value Object | `MessageId` | UUID |
| Value Object | `ChatId` | Посилання на чат (cross-context ref, тільки ID) |
| Value Object | `AuthorId` | `UserId` автора |
| Value Object | `MessageContent` | `{ text?: string, attachments: Attachment[] }` |
| Value Object | `Attachment` | `{ type: Image\|File\|Audio\|Video, url, sizeBytes, fileName? }` |
| Value Object | `ReplyReference` | `{ replyToId: MessageId }` або `null` |
| Value Object | `ForwardReference` | `{ originalMessageId, originalChatId }` або `null` |
| Value Object | `MessageStatus` | `{ isRetracted: bool, retractedAt?, retractedBy? }` |
| Entity | `Reaction` | `{ reactionId, memberId, emoji, addedAt }` |

**Рішення по вкладеннях:** `Attachment` — Value Object всередині `MessageContent`. Файли зберігаються в окремому blob-storage (S3 / MinIO), `Message` зберігає лише URL.

**Рішення по `ForwardedMessage`:** Поле `ForwardReference` у `Message` (не окрема сутність). Якщо `forwardReference != null` — це переслане повідомлення.

**Read Model: `HiddenMessage`**
`{ messageId: MessageId, userId: UserId }` — не агрегат, не публікує подій.

**Domain Events:**
- `MessageSent { messageId, chatId, authorId, content, replyTo?, forwardFrom?, sentAt }`
- `MessageRetracted { messageId, chatId, retractedBy, at }`
- `ReactionAdded { messageId, chatId, memberId, emoji, at }`
- `ReactionRemoved { messageId, chatId, memberId, emoji, at }`

**Інваріанти:**
- `MessageContent` не може бути повністю порожнім (або `text` не пустий, або `attachments` не пустий).
- `Reaction.emoji` — з фіксованого набору (emoji set — вирішено нижче).
- Максимум реакцій на `Message` від одного `Member`: 1 (2 для Premium).
- `Reaction` і `Reply` неможливі для `isRetracted = true` повідомлення.
- `ReplyReference.replyToId` має вказувати на `Message` у тому ж `Chat`.

#### Read Model: `MessageReadReceipt`
`{ messageId: MessageId, memberId: UserId, readAt: DateTime }`
У `BroadcastChannel` — лише лічильник `{ messageId, readCount }`, не індивідуальні записи.

---

### Context: Presence

#### Aggregate: `UserPresence`
**Aggregate Root:** `UserPresence`

| Тип | Назва | Опис |
|---|---|---|
| Value Object | `UserId` | Cross-context ref |
| Value Object | `OnlineStatus` | `Online \| Offline` |
| Value Object | `LastSeenAt` | `DateTime \| null` (null якщо прихований налаштуваннями) |

**`TypingIndicator`** — не агрегат, не персистується. Тільки real-time через Redis pub/sub.
Структура: `{ chatId, userId, expiresAt }` — TTL 5 секунд, автоматично зникає.

**Domain Events:**
- `UserCameOnline { userId, at }`
- `UserWentOffline { userId, lastSeenAt }`

---

### Context: Notifications

#### Aggregate: `Notification`
**Aggregate Root:** `Notification`

| Тип | Назва | Опис |
|---|---|---|
| Value Object | `NotificationId` | UUID |
| Value Object | `RecipientId` | `UserId` отримувача |
| Value Object | `NotificationType` | `NewMessage \| MentionInGroup \| MemberJoined \| MemberKicked` |
| Value Object | `NotificationPayload` | `{ chatId, messageId?, actorId? }` |
| Value Object | `NotificationStatus` | `Pending \| Delivered \| Read` |

**Domain Events:**
- `NotificationCreated { notificationId, recipientId, type, payload, at }`
- `NotificationRead { notificationId, at }`

**Інваріант:** `Notification` — immutable після створення, змінюється лише `status`.

---

## Emoji Set для Reactions

**Рішення:** Фіксований набір з 8 emoji (розширюємо пізніше):

| Emoji | Назва |
|---|---|
| 👍 | `like` |
| ❤️ | `heart` |
| 😂 | `laugh` |
| 😮 | `wow` |
| 😢 | `sad` |
| 😡 | `angry` |
| 🔥 | `fire` |
| 👎 | `dislike` |

Зберігається як string-enum у коді. Кастомні emoji — TODO для Premium.

---

## TODO / Відкриті питання

- [ ] `TransferOwnership` — команда для передачі ролі `Owner`. Додати до Commands-таблиці.
- [ ] Push-повідомлення (FCM/APNS) — деталі на етапі Notifications context.
- [ ] Відеоповідомлення (`Audio`, `Video`) — максимальний розмір `Attachment`? Ліміти зберігання.
- [ ] Кастомні emoji для реакцій (Premium) — окремий TODO для майбутнього.
