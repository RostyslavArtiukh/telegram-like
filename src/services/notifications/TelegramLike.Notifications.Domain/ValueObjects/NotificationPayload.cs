namespace TelegramLike.Notifications.Domain.ValueObjects;

public sealed record NotificationPayload
{
    public Guid ChatId { get; }
    public Guid? MessageId { get; }
    public Guid? TriggeredByUserId { get; }

    private NotificationPayload(Guid chatId, Guid? messageId, Guid? triggeredByUserId)
    {
        if (chatId == Guid.Empty)
            throw new DomainException("ChatId cannot be empty.");

        ChatId = chatId;
        MessageId = messageId;
        TriggeredByUserId = triggeredByUserId;
    }

    public static NotificationPayload ForNewMessage(Guid chatId, Guid messageId, Guid triggeredByUserId)
    {
        if (messageId == Guid.Empty) throw new DomainException("MessageId required.");
        if (triggeredByUserId == Guid.Empty) throw new DomainException("TriggeredByUserId required.");
        return new NotificationPayload(chatId, messageId, triggeredByUserId);
    }

    public static NotificationPayload ForMention(Guid chatId, Guid messageId, Guid triggeredByUserId)
        => ForNewMessage(chatId, messageId, triggeredByUserId);

    public static NotificationPayload ForMemberJoined(Guid chatId, Guid triggeredByUserId)
    {
        if (triggeredByUserId == Guid.Empty) throw new DomainException("TriggeredByUserId required.");
        return new NotificationPayload(chatId, messageId: null, triggeredByUserId);
    }

    public static NotificationPayload ForMemberKicked(Guid chatId, Guid triggeredByUserId)
        => ForMemberJoined(chatId, triggeredByUserId);

    public static NotificationPayload FromStorage(Guid chatId, Guid? messageId, Guid? triggeredByUserId)
        => new(chatId, messageId, triggeredByUserId);
}
