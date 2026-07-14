namespace TelegramLike.Simulator;

/// <summary>
/// Консольний «суфлер»: кожен бот пише своїм кольором, помилки — червоним,
/// плюс лічильники для періодичної статистики. Це і є глядацька зала —
/// решту вистави дивимось у Grafana/RabbitMQ/Jaeger.
/// </summary>
public sealed class SimulationLog
{
    private static readonly ConsoleColor[] Palette =
    [
        ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Magenta,
        ConsoleColor.Blue, ConsoleColor.DarkCyan, ConsoleColor.DarkGreen, ConsoleColor.DarkYellow,
        ConsoleColor.DarkMagenta, ConsoleColor.Gray,
    ];

    private readonly object _gate = new();
    private long _messages, _reactions, _reads, _historyViews, _retracts, _errors;

    public void Bot(string username, string text)
        => WriteLine(ColorFor(username), $"{Timestamp()} {username,-14} {text}");

    public void Info(string text)
        => WriteLine(ConsoleColor.White, $"{Timestamp()} {text}");

    public void Error(string username, string message)
    {
        Interlocked.Increment(ref _errors);
        WriteLine(ConsoleColor.Red, $"{Timestamp()} {username,-14} ⚠ {message}");
    }

    public void CountMessage() => Interlocked.Increment(ref _messages);
    public void CountReaction() => Interlocked.Increment(ref _reactions);
    public void CountRead() => Interlocked.Increment(ref _reads);
    public void CountHistoryView() => Interlocked.Increment(ref _historyViews);
    public void CountRetract() => Interlocked.Increment(ref _retracts);

    public string Stats() =>
        $"повідомлень {Interlocked.Read(ref _messages)} · " +
        $"реакцій {Interlocked.Read(ref _reactions)} · " +
        $"прочитань {Interlocked.Read(ref _reads)} · " +
        $"переглядів історії {Interlocked.Read(ref _historyViews)} · " +
        $"відкликань {Interlocked.Read(ref _retracts)} · " +
        $"помилок {Interlocked.Read(ref _errors)}";

    private static string Timestamp() => $"[{DateTime.Now:HH:mm:ss}]";

    private static ConsoleColor ColorFor(string username)
    {
        var hash = 0;
        foreach (var c in username) hash = hash * 31 + c;
        return Palette[Math.Abs(hash) % Palette.Length];
    }

    private void WriteLine(ConsoleColor color, string line)
    {
        lock (_gate)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = previous;
        }
    }
}
