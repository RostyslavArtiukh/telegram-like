using System.Collections.Concurrent;
using TelegramLike.Client.Chats;
using TelegramLike.Contracts.Realtime;

namespace TelegramLike.Simulator;

/// <summary>
/// Актор вистави: живе в циклі «пауза → випадкова дія». Пише повідомлення
/// (з typing-індикатором), відповідає на чужі, реагує, читає, гортає історію,
/// зрідка відкликає своє. Чужі повідомлення бачить так само, як справжній
/// клієнт — через SignalR-пуші realtime-хаба; паралельно тримає presence
/// heartbeat (20с, як MAUI/Web — Redis TTL 30с).
/// </summary>
public sealed class BotActor(
    BotClient bot, IReadOnlyList<ChatHandle> chats, SimulatorOptions options, SimulationLog log)
{
    private const int RecentLimit = 15;

    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<MessageSentPush>> _recentIncoming = new();
    private readonly ConcurrentQueue<Guid> _ownMessages = new();

    // «Пам'ять» актора: що вже відкликано / на що реагував / що читав. Без неї
    // повторна реакція чи read того самого повідомлення ловить семантичний 400.
    private readonly ConcurrentDictionary<Guid, byte> _retracted = new();
    private readonly ConcurrentDictionary<Guid, byte> _reactedTo = new();
    private readonly ConcurrentDictionary<Guid, byte> _markedRead = new();

    public async Task RunAsync(CancellationToken ct)
    {
        bot.Realtime.MessageSent += OnMessageSent;
        bot.Realtime.MessageRetracted += OnMessageRetracted;
        try
        {
            foreach (var chat in chats)
                await bot.Realtime.JoinChatAsync(chat.ChatId, ct);

            await Task.WhenAll(ActionLoopAsync(ct), HeartbeatLoopAsync(ct));
        }
        finally
        {
            bot.Realtime.MessageSent -= OnMessageSent;
            bot.Realtime.MessageRetracted -= OnMessageRetracted;
            await ShutdownAsync();
        }
    }

    private void OnMessageSent(MessageSentPush push)
    {
        if (push.AuthorId == bot.UserId) return;
        Remember(push);
    }

    private void OnMessageRetracted(MessageRetractedPush push)
        => _retracted.TryAdd(push.MessageId, 0);

    private void Remember(MessageSentPush push)
    {
        var queue = _recentIncoming.GetOrAdd(push.ChatId, _ => new ConcurrentQueue<MessageSentPush>());
        queue.Enqueue(push);
        while (queue.Count > RecentLimit) queue.TryDequeue(out _);
    }

    private async Task ActionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var actionName = "пауза";
            try
            {
                await Task.Delay(NextDelay(), ct);
                var (name, action) = PickAction();
                actionName = name;
                await action(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Одна невдала дія не знімає актора з вистави — resilience pipeline
                // своє відпрацював, ми лиш фіксуємо і граємо далі.
                log.Error(bot.Username, $"{actionName}: {ex.Message}");
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await bot.Presence.HeartbeatAsync(bot.UserId, ct);
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.Error(bot.Username, $"heartbeat: {ex.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(20), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private TimeSpan NextDelay()
    {
        var seconds = options.MinActionDelaySeconds +
                      Random.Shared.NextDouble() * (options.MaxActionDelaySeconds - options.MinActionDelaySeconds);
        return TimeSpan.FromSeconds(Math.Max(0.2, seconds));
    }

    private (string Name, Func<CancellationToken, Task> Action) PickAction() => Random.Shared.Next(100) switch
    {
        < 50 => ("повідомлення", SendMessageAsync),
        < 70 => ("реакція", (Func<CancellationToken, Task>)ReactToRecentAsync),
        < 85 => ("прочитання", MarkRecentAsReadAsync),
        < 95 => ("історія", BrowseHistoryAsync),
        _ => ("відкликання", RetractOwnMessageAsync),
    };

    private async Task SendMessageAsync(CancellationToken ct)
    {
        var sendable = chats.Where(c => c.CanSend).ToList();
        if (sendable.Count == 0) return;
        var chat = sendable[Random.Shared.Next(sendable.Count)];

        Guid? replyTo = null;
        string text;
        if (chat.Type == ChatType.Broadcast)
        {
            text = ScriptLibrary.RandomAnnouncement();
        }
        else if (TryPickRecent(chat.ChatId, NotRetracted, out var incoming) && Random.Shared.NextDouble() < 0.6)
        {
            replyTo = incoming.MessageId;
            text = ScriptLibrary.RandomReply();
        }
        else
        {
            text = ScriptLibrary.RandomStarter();
        }

        // «Друкує…» перед відправкою — щоб у стрічці подій були й typing-сигнали.
        await bot.Presence.StartTypingAsync(bot.UserId, chat.ChatId, ct);
        try
        {
            await Task.Delay(Random.Shared.Next(800, 2500), ct);
            var messageId = await bot.Messaging.SendMessageAsync(
                bot.UserId, chat.ChatId, text,
                isBroadcast: chat.Type == ChatType.Broadcast,
                replyToMessageId: replyTo,
                cancellationToken: ct);

            _ownMessages.Enqueue(messageId);
            while (_ownMessages.Count > RecentLimit) _ownMessages.TryDequeue(out _);

            log.Bot(bot.Username, $"{(replyTo is null ? "✉" : "↩")} {chat.Name}: \"{Truncate(text)}\"");
            log.CountMessage();
        }
        finally
        {
            await SafeStopTypingAsync(chat.ChatId);
        }
    }

    private async Task ReactToRecentAsync(CancellationToken ct)
    {
        // Реакції лишаємо поза broadcast-каналом — там глядачі, а не співрозмовники.
        if (!TryPickRecentAnywhere(
                c => c.Type != ChatType.Broadcast,
                m => NotRetracted(m) && !_reactedTo.ContainsKey(m.MessageId),
                out var chat, out var incoming))
        {
            await SendMessageAsync(ct); // нема на що реагувати — краще заговорити
            return;
        }

        // Позначаємо ДО виклику: якщо сервіс усе ж відмовить, повторно не мучимо.
        _reactedTo.TryAdd(incoming.MessageId, 0);
        var emoji = ScriptLibrary.RandomReaction();
        await bot.Messaging.AddReactionAsync(bot.UserId, incoming.MessageId, emoji, ct);
        log.Bot(bot.Username, $"❤ реакція {emoji} у «{chat.Name}»");
        log.CountReaction();
    }

    private async Task MarkRecentAsReadAsync(CancellationToken ct)
    {
        if (!TryPickRecentAnywhere(
                _ => true,
                m => NotRetracted(m) && !_markedRead.ContainsKey(m.MessageId),
                out var chat, out var incoming))
        {
            await SendMessageAsync(ct);
            return;
        }

        _markedRead.TryAdd(incoming.MessageId, 0);
        await bot.Messaging.MarkAsReadAsync(bot.UserId, incoming.MessageId, ct);
        log.Bot(bot.Username, $"👁 прочитав у «{chat.Name}»");
        log.CountRead();
    }

    private async Task BrowseHistoryAsync(CancellationToken ct)
    {
        var chat = chats[Random.Shared.Next(chats.Count)];
        var page = await bot.Messaging.GetChatMessagesAsync(bot.UserId, chat.ChatId, pageSize: 30, cancellationToken: ct);

        // Історія підживлює пам'ять актора — стане чим оперувати реакціям і reply,
        // навіть якщо realtime-пуш десь загубився (relay без dedup — це нормально).
        foreach (var message in page.Items.Where(m => m.AuthorId != bot.UserId && !m.IsRetracted).Take(5))
            Remember(new MessageSentPush(message.MessageId, chat.ChatId, message.AuthorId));

        log.Bot(bot.Username, $"📜 погортав «{chat.Name}» ({page.Items.Count} повідомлень)");
        log.CountHistoryView();
    }

    private async Task RetractOwnMessageAsync(CancellationToken ct)
    {
        if (!_ownMessages.TryDequeue(out var messageId))
        {
            await SendMessageAsync(ct);
            return;
        }

        _retracted.TryAdd(messageId, 0);
        await bot.Messaging.RetractMessageAsync(bot.UserId, messageId, retractedByModerator: false, ct);
        log.Bot(bot.Username, "🗑 відкликав своє повідомлення");
        log.CountRetract();
    }

    private bool NotRetracted(MessageSentPush push) => !_retracted.ContainsKey(push.MessageId);

    private bool TryPickRecent(Guid chatId, Func<MessageSentPush, bool> messageFilter, out MessageSentPush push)
    {
        push = default!;
        if (!_recentIncoming.TryGetValue(chatId, out var queue)) return false;
        var snapshot = queue.Where(messageFilter).ToArray();
        if (snapshot.Length == 0) return false;
        push = snapshot[Random.Shared.Next(snapshot.Length)];
        return true;
    }

    private bool TryPickRecentAnywhere(
        Func<ChatHandle, bool> chatFilter, Func<MessageSentPush, bool> messageFilter,
        out ChatHandle chat, out MessageSentPush push)
    {
        chat = default!;
        push = default!;
        var candidates = chats.Where(chatFilter).OrderBy(_ => Random.Shared.Next()).ToList();
        foreach (var candidate in candidates)
        {
            if (TryPickRecent(candidate.ChatId, messageFilter, out push))
            {
                chat = candidate;
                return true;
            }
        }
        return false;
    }

    private async Task SafeStopTypingAsync(Guid chatId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await bot.Presence.StopTypingAsync(bot.UserId, chatId, timeout.Token); }
        catch { /* сигнальний виклик — не критично */ }
    }

    private async Task ShutdownAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await bot.Presence.GoOfflineAsync(bot.UserId, timeout.Token); } catch { }
        try { await bot.Realtime.DisconnectAsync(timeout.Token); } catch { }
        log.Info($"   ⏻ {bot.Username} пішов зі сцени");
    }

    private static string Truncate(string text)
        => text.Length <= 48 ? text : text[..45] + "…";
}
