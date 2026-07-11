using MediatR;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Application.Commands.FanoutChatNotification;

/// Internal command dispatched from integration-event consumers. Recipients are pre-resolved
/// by the publishing context so Notifications has no cross-context queries.
public sealed record FanoutChatNotificationCommand(
    Guid ChatId,
    Guid TriggeredByUserId,
    NotificationType Type,
    IReadOnlyList<Guid> Recipients,
    Guid SourceEventId,
    Guid? MessageId = null) : IRequest;
