using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Notifications;

/// Notifies Web (or any subscriber) that the listed users' unread counts may
/// have shifted (a fanout created new notifications, or someone marked notifications
/// as read). The subscriber should refetch the count via the BFF; we don't include
/// the new count in the payload to avoid stale-read races between concurrent ops.
[IntegrationEventName("notifications.unread-count-changed.v1")]
public sealed record UnreadCountChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    IReadOnlyList<Guid> UserIds) : IIntegrationEvent;
