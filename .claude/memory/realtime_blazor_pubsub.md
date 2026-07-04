---
name: realtime-blazor-pubsub
description: Як real-time UI оновлення працюють у Blazor Server через in-memory pubsub поверх RabbitMQ
metadata: 
  node_type: memory
  type: project
  originSessionId: c86df29a-c998-45fb-8ef5-72540737621d
---

День 17 (2026-05-30): real-time typing indicator реалізовано БЕЗ окремого SignalR Hub.

**Why:** Blazor Server вже використовує SignalR під капотом для circuit (browser ↔ server). Окремий Hub дублює інфраструктуру і вимагає client-side JS. Простіше: серверний C# pubsub між MassTransit consumer і Razor компонентом у тому ж процесі. Компонент робить `await InvokeAsync(StateHasChanged)` — Blazor сам пушить рендер у браузер через існуючий circuit.

**How to apply:**
- Для **real-time UI events** з backend сервісів → не створюй SignalR Hub. Використовуй цей патерн:
  1. Backend сервіс публікує integration event у RabbitMQ
  2. Web має MassTransit consumer (`AddInfrastructure(cfg, bus => bus.AddConsumer<...>())`)
  3. Consumer викликає shared in-memory pubsub (`ITypingPubSub` як приклад)
  4. Razor компонент на init підписується (`pubsub.Subscribe(key, callback)`), на dispose — відписується
  5. Callback оновлює state і робить `InvokeAsync(StateHasChanged)`

**Файли (приклад typing):**
- [Contracts/Presence/UserTypingIntegrationEvent.cs](src/TelegramLike.Contracts/Presence/UserTypingIntegrationEvent.cs)
- [Presence StartTypingCommandHandler](src/services/presence/TelegramLike.Presence.Application/Commands/StartTyping/StartTypingCommandHandler.cs) — `IPublishEndpoint.Publish`
- [Web/Services/Typing/ITypingPubSub.cs](src/TelegramLike.Web/Services/Typing/ITypingPubSub.cs) + `TypingPubSub.cs` — `ConcurrentDictionary<chatId, ConcurrentDictionary<token, callback>>`
- [Web/Services/Typing/UserTypingConsumer.cs](src/TelegramLike.Web/Services/Typing/UserTypingConsumer.cs)
- [Web/Components/Pages/ChatView.razor](src/TelegramLike.Web/Components/Pages/ChatView.razor) — `_typingSubscription = TypingPubSub.Subscribe(ChatId, OnRemoteTypingAsync);`

**Інші ключові моменти:**
- **Direct publish (без outbox)** — typing ephemeral, лосс event = трохи затримки UI. Outbox для transactional consistency треба тільки для critical events (зміни стану).
- **TTL у клієнта:** browser отримує `{userId, typing}` push, додає у dictionary `_typingExpiry[userId] = now + 5s`. Тимер 3 сек sweep'ить expired. Серверний Redis TTL = 5 сек узгоджений.
- **Throttle на стороні publisher** — `ChatView.OnInput` шле StartTyping не частіше 1 раз / 2 сек (бо Redis TTL = 5s, тримаємо ключ alive перевипуском).

**Web horizontal scale — ВИРІШЕНО ([TL-63], 2026-07-04):**
- Проблема: `ConfigureEndpoints` дає **одну спільну durable-чергу на консюмер** → з кількома Web-репліками RabbitMQ round-robin'ить події → лише одна репліка отримує кожну, юзери на інших репліках не бачать real-time.
- Фікс (БЕЗ Redis — RabbitMQ exchange вже вміє fanout): кожна Web-репліка = **власна auto-delete черга**. У `Web/Program.cs` кожен `AddConsumer<>().Endpoint(e => { e.Temporary = true; e.InstanceId = busInstanceId; })`, де `busInstanceId = Guid.NewGuid("N")` на процес. Обидві черги біндяться до того ж message-type exchange → кожна подія копіюється в усі репліки; `Temporary` = non-durable+auto-delete (черга зникає коли pod вмирає).
- **Друга половина — sticky sessions.** Blazor Server circuit тримається в пам'яті одного pod'а → LB мусить пінити браузер до того ж pod'а. Це НЕ Redis backplane (той для SignalR Hub-броадкасту, у Blazor Server його немає). Зроблено через ingress-nginx cookie-affinity (`k8s/32-web-ingress.yaml`, cookie `tl-affinity`).
- 5 backend-сервісів лишають **спільні durable черги** — read-model має обробити подію раз, а не раз-на-репліку. Presence/identity/chats/messaging уже горизонтально масштабовані без змін.
- Каверза при in-place upgrade: старі durable базові черги лишаються orphan (0 консюмерів) і накопичують меседжі; на свіжому деплої їх нема (RabbitMQ storage ефемерний). Одноразово чистяться `rabbitmqctl delete_queue <name> --if-unused`.

**Обмеження, що лишились:**
- **Тільки typing/new-message/reactions/presence зараз** — для нових real-time типів той самий рецепт (нижче).

**Чому НЕ окремий SignalR Hub:**
- Дублює інфру (Blazor circuit вже SignalR)
- Потрібен client-side `Microsoft.AspNetCore.SignalR.Client` + JS — для Blazor Server це зайва робота
- Hub корисний для **interactive WASM** або зовнішніх клієнтів (mobile app etc.) — там Blazor circuit не існує
- Pattern буде потрібен якщо колись додамо mobile app — тоді треба буде Hub поверх. Поки YAGNI.

## Як додати real-time для нового event типу (рецепт)
1. Backend service: додай integration event у `Contracts/<Context>/`, publish-нь у відповідному handler через `IPublishEndpoint`.
2. Web: створи `IXPubSub` + impl за паттерном `TypingPubSub`.
3. Web: створи `XConsumer : IConsumer<XIntegrationEvent>` що викликає pubsub.
4. Реєструй у `Program.cs`: `AddSingleton<IXPubSub, XPubSub>()` + у `AddInfrastructure(..., bus => bus.AddConsumer<XConsumer>())`.
5. Razor компонент: `Subscribe(key, callback)` у `OnInitializedAsync`, `Dispose` у `DisposeAsync`. Callback: оновити state + `await InvokeAsync(StateHasChanged)`.
