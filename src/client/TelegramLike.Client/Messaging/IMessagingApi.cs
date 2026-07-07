namespace TelegramLike.Client.Messaging;

public interface IMessagingApi
{
    Task<Guid> SendMessageAsync(
        Guid authorUserId,
        Guid chatId,
        string? text,
        IReadOnlyList<Guid> recipients,
        bool isBroadcast,
        IReadOnlyList<OutgoingAttachment>? attachments = null,
        Guid? replyToMessageId = null,
        Guid? forwardOriginalMessageId = null,
        Guid? forwardOriginalChatId = null,
        CancellationToken cancellationToken = default);

    Task<ChatMessage?> GetMessageByIdAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);

    Task<ChatMessagePage> GetChatMessagesAsync(
        Guid userId,
        Guid chatId,
        DateTime? before = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task AddReactionAsync(Guid userId, Guid messageId, ReactionEmoji emoji, bool actorIsPremium, CancellationToken cancellationToken = default);
    Task RemoveReactionAsync(Guid userId, Guid messageId, ReactionEmoji emoji, CancellationToken cancellationToken = default);

    Task RetractMessageAsync(Guid actorUserId, Guid messageId, bool actorIsModerator, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid userId, Guid messageId, bool isBroadcast, CancellationToken cancellationToken = default);

    Task HideMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
}
