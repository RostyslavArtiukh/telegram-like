using TelegramLike.Client.Chats;

namespace TelegramLike.Simulator;

/// <summary>Чат очима конкретного бота: куди слати і чи можна писати. Кому саме —
/// вирішує Messaging зі свого read-model ([TL-118]), боту це знати не треба.</summary>
public sealed record ChatHandle(
    Guid ChatId,
    string Name,
    ChatType Type,
    bool CanSend);

/// <summary>
/// Сцена вистави: спільна група + broadcast-канал (усі), дві менші групи
/// (половини трупи) та дірект-пари i↔i+1. Повторний запуск не плодить дублікати:
/// групи шукаються за назвою серед чатів власника, дірект-create ідемпотентний
/// на боці Chats.
/// </summary>
public static class ChatTopology
{
    public const string CommonGroupName = "Симуляція · Загальний";
    public const string WorkGroupName = "Симуляція · Робота";
    public const string LeisureGroupName = "Симуляція · Дозвілля";
    public const string BroadcastName = "Симуляція · Оголошення";

    public static async Task<IReadOnlyList<List<ChatHandle>>> BuildAsync(
        IReadOnlyList<BotClient> bots, SimulationLog log, CancellationToken ct)
    {
        var handles = bots.Select(_ => new List<ChatHandle>()).ToList();
        var indexOf = bots.Select((bot, i) => (bot, i)).ToDictionary(x => x.bot, x => x.i);

        // (чат, власник, учасники) — групи й канал; членство наповнюється joins нижче.
        var groupPlans = new List<(string Name, ChatType Type, BotClient Owner, IReadOnlyList<BotClient> Members)>
        {
            (CommonGroupName, ChatType.Group, bots[0], bots),
            (BroadcastName, ChatType.Broadcast, bots[0], bots),
        };

        if (bots.Count >= 4)
        {
            var firstHalf = bots.Take(bots.Count / 2).ToList();
            var secondHalf = bots.Skip(bots.Count / 2).ToList();
            groupPlans.Add((WorkGroupName, ChatType.Group, firstHalf[0], firstHalf));
            groupPlans.Add((LeisureGroupName, ChatType.Group, secondHalf[0], secondHalf));
        }

        foreach (var (name, type, owner, members) in groupPlans)
        {
            var chatId = await EnsureChatAsync(owner, type, name, log, ct);

            foreach (var member in members.Where(m => m != owner))
            {
                try
                {
                    await member.Chats.JoinChatAsync(member.UserId, chatId, ct);
                }
                catch (HttpRequestException)
                {
                    // Найімовірніше вже учасник із минулого запуску — join не ідемпотентний.
                }
            }

            foreach (var member in members)
            {
                var canSend = type != ChatType.Broadcast || member == owner;
                handles[indexOf[member]].Add(new ChatHandle(chatId, name, type, canSend));
            }
        }

        // Дірект-пари по колу: кожен бот у двох діалогах (з сусідами).
        var pairCount = bots.Count == 2 ? 1 : bots.Count;
        for (var i = 0; i < pairCount; i++)
        {
            var a = bots[i];
            var b = bots[(i + 1) % bots.Count];
            var chatId = await a.Chats.CreateDirectChatAsync(a.UserId, b.UserId, ct);
            handles[indexOf[a]].Add(new ChatHandle(chatId, $"діалог з {b.Username}", ChatType.Direct, true));
            handles[indexOf[b]].Add(new ChatHandle(chatId, $"діалог з {a.Username}", ChatType.Direct, true));
        }

        return handles;
    }

    private static async Task<Guid> EnsureChatAsync(
        BotClient owner, ChatType type, string name, SimulationLog log, CancellationToken ct)
    {
        var mine = await owner.Chats.GetMyChatsAsync(owner.UserId, ct);
        var existing = mine.FirstOrDefault(c => c.Type == type && c.Name == name);
        if (existing is not null)
        {
            log.Info($"   ↺ «{name}» вже існує — використовуємо");
            return existing.ChatId;
        }

        var chatId = type == ChatType.Broadcast
            ? await owner.Chats.CreateBroadcastChannelAsync(owner.UserId, name, ct)
            : await owner.Chats.CreateGroupChatAsync(owner.UserId, name, ct);
        log.Info($"   + «{name}» створено (власник {owner.Username})");
        return chatId;
    }
}
