namespace TelegramLike.Client.Messaging;

public interface IMessagingApi
{
    Task<Guid> SendMessageAsync(
        Guid authorUserId,
        Guid chatId,
        string? text,
        IReadOnlyList<Guid> recipients,
        bool isBroadcast,
        IReadOnlyList<SendMessageAttachmentContract>? attachments = null,
        Guid? replyToMessageId = null,
        Guid? forwardOriginalMessageId = null,
        Guid? forwardOriginalChatId = null,
        CancellationToken ct = default);

    Task<MessageContract?> GetMessageByIdAsync(Guid userId, Guid messageId, CancellationToken ct = default);

    Task<MessagePageContract> GetChatMessagesAsync(
        Guid userId,
        Guid chatId,
        DateTime? before = null,
        int pageSize = 50,
        CancellationToken ct = default);

    Task AddReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, bool actorIsPremium, CancellationToken ct = default);
    Task RemoveReactionAsync(Guid userId, Guid messageId, EmojiContract emoji, CancellationToken ct = default);

    Task RetractMessageAsync(Guid actorUserId, Guid messageId, bool actorIsModerator, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid userId, Guid messageId, bool isBroadcast, CancellationToken ct = default);

    Task HideMessageAsync(Guid userId, Guid messageId, CancellationToken ct = default);
}
