---
name: chats-persistence
description: Як зберігається агрегат Chat у MongoDB — дві колекції з multi-document транзакцією
metadata: 
  node_type: memory
  type: project
  originSessionId: 7cb0409c-41f8-48f6-a1f5-690d5ce7f4eb
  modified: 2026-07-28T17:12:23.854Z
---

Агрегат `Chat` (з вкладеним `Member`) зберігається у двох MongoDB-колекціях: `chats` (метадані) + `chat_members` (один документ на кожного учасника).

**Why:** GroupChat і BroadcastChannel можуть мати сотні учасників — embedded масив розпухне; FormerMembers (Left/Kicked/Banned) теж зберігаються, тож масив росте; окрема колекція дає незалежні індекси по `status`/`role`. Це зафіксовано в `docs/database.md` ще в День 2.

**How to apply:**
- `ChatRepository.AddAsync` і `UpdateAsync` відкривають session через `IMongoClient.StartSessionAsync` і виконують зміни в `WithTransactionAsync` — інакше можна отримати неузгоджений стан при збої.
- `UpdateAsync` робить `BulkWrite` з `ReplaceOneModel<ChatMemberDocument>{IsUpsert=true}` по `Member.Id` — Member не видаляються при Leave/Kick/Ban, лише змінюється `Status`.
- **Рівно один рядок на пару (chat, user).** Оскільки upsert іде по `Member.Id` і нічого не видаляє, повторний вхід МУСИТЬ оживляти наявний рядок (`Member.Rejoin`), а не створювати новий — інакше `chat_members` росла на документ за кожен цикл leave→join. Дублікати робили `FindAnyMember` (це `FirstOrDefault`) залежним від порядку Mongo, через що `Ban` міг позначити застарілий `Left`-рядок як Banned, лишивши живий рядок Active — бан мовчки не діяв. Підтверджено integration-тестом до фіксу (3 рядки після 2 циклів; `statuses=[Banned,Left,Active]` при активному учаснику). Backstop — унікальний індекс `(ChatId, UserId)` у `ChatIndexInitializer`, який перед створенням прибирає дублікати (бан > активний > найновіший `JoinedAt`), бо Mongo не збудує unique-індекс на вже забрудненій БД.
- MongoDB має бути запущений як replica set (`--replSet rs0`) — інакше транзакції впадуть з помилкою `Transaction numbers are only allowed on a replica set member`. У `docker-compose.yml` healthcheck автоматично виконує `rs.initiate()` при першому старті.
- ConnectionString у `appsettings.json` має містити `?replicaSet=rs0&directConnection=true`.
- Якщо в майбутньому додамо auth до Mongo з replica set — знадобиться `keyFile` mount у compose.
