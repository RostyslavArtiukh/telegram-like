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
            ActorId: dto.ActorId,
            Status: ToContract(dto.Status),
            CreatedAt: dto.CreatedAt,
            ReadAt: dto.ReadAt);

    private static NotificationTypeContract ToContract(DomainType type) => type switch
    {
        DomainType.NewMessage => NotificationTypeContract.NewMessage,
        DomainType.MentionInGroup => NotificationTypeContract.MentionInGroup,
        DomainType.MemberJoined => NotificationTypeContract.MemberJoined,
        DomainType.MemberKicked => NotificationTypeContract.MemberKicked,
        _ => throw new InvalidOperationException($"Unknown notification type: {type}")
    };

    private static NotificationStatusContract ToContract(DomainStatus status) => status switch
    {
        DomainStatus.Pending => NotificationStatusContract.Pending,
        DomainStatus.Delivered => NotificationStatusContract.Delivered,
        DomainStatus.Read => NotificationStatusContract.Read,
        _ => throw new InvalidOperationException($"Unknown notification status: {status}")
    };
}
