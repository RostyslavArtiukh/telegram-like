using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Application.Queries;
using DomainStatus = TelegramLike.Notifications.Domain.ValueObjects.NotificationStatus;
using DomainType = TelegramLike.Notifications.Domain.ValueObjects.NotificationType;

namespace TelegramLike.Notifications.Api.Mapping;

internal static class ContractMappers
{
    public static NotificationFeedApiDto ToContract(this NotificationFeedDto feed) =>
        new(feed.Items.Select(ToContract).ToList(), feed.NextCursor);

    public static NotificationApiDto ToContract(this NotificationDto dto) =>
        new(
            Id: dto.Id,
            RecipientId: dto.RecipientId,
            Type: ToContract(dto.Type),
            ChatId: dto.ChatId,
            MessageId: dto.MessageId,
            TriggeredByUserId: dto.TriggeredByUserId,
            Status: ToContract(dto.Status),
            CreatedAt: dto.CreatedAt,
            ReadAt: dto.ReadAt);

    private static NotificationType ToContract(DomainType type) => type switch
    {
        DomainType.NewMessage => NotificationType.NewMessage,
        DomainType.MentionInGroup => NotificationType.MentionInGroup,
        DomainType.MemberJoined => NotificationType.MemberJoined,
        DomainType.MemberKicked => NotificationType.MemberKicked,
        _ => throw new InvalidOperationException($"Unknown notification type: {type}")
    };

    private static NotificationStatus ToContract(DomainStatus status) => status switch
    {
        DomainStatus.Pending => NotificationStatus.Pending,
        DomainStatus.Delivered => NotificationStatus.Delivered,
        DomainStatus.Read => NotificationStatus.Read,
        _ => throw new InvalidOperationException($"Unknown notification status: {status}")
    };
}
