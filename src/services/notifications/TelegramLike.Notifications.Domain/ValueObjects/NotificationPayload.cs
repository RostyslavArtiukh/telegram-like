namespace TelegramLike.Notifications.Domain.ValueObjects;

public sealed record NotificationPayload
{
    public Guid ChatId { get; }
    public Guid? MessageId { get; }
    public Guid? ActorId { get; }

    private NotificationPayload(Guid chatId, Guid? messageId, Guid? actorId)
    {
        if (chatId == Guid.Empty)
            throw new ArgumentException("ChatId cannot be empty.", nameof(chatId));

        ChatId = chatId;
        MessageId = messageId;
        ActorId = actorId;
    }

    public static NotificationPayload ForNewMessage(Guid chatId, Guid messageId, Guid actorId)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("MessageId required.", nameof(messageId));
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId required.", nameof(actorId));
        return new NotificationPayload(chatId, messageId, actorId);
    }

    public static NotificationPayload ForMention(Guid chatId, Guid messageId, Guid actorId)
        => ForNewMessage(chatId, messageId, actorId);

    public static NotificationPayload ForMemberJoined(Guid chatId, Guid actorId)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId required.", nameof(actorId));
        return new NotificationPayload(chatId, messageId: null, actorId);
    }

    public static NotificationPayload ForMemberKicked(Guid chatId, Guid actorId)
        => ForMemberJoined(chatId, actorId);

    public static NotificationPayload Reconstitute(Guid chatId, Guid? messageId, Guid? actorId)
        => new(chatId, messageId, actorId);
}
