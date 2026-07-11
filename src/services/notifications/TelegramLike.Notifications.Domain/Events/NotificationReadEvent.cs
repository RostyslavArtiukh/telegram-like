using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Notifications.Domain.Events;

public sealed record NotificationReadEvent(Guid NotificationId, Guid RecipientId, DateTime ReadAt) : IChangeEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
