namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

public sealed class OutgoingEventsSenderOptions
{
    public int PollIntervalSeconds { get; set; } = 2;

    public int BatchSize { get; set; } = 50;

    public int MaxRetries { get; set; } = 5;
}
