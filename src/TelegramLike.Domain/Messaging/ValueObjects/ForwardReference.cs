namespace TelegramLike.Domain.Messaging.ValueObjects;

public sealed record ForwardReference(Guid OriginalMessageId, Guid OriginalChatId)
{
    public static ForwardReference From(Guid originalMessageId, Guid originalChatId)
    {
        if (originalMessageId == Guid.Empty)
            throw new ArgumentException("OriginalMessageId cannot be empty.", nameof(originalMessageId));
        if (originalChatId == Guid.Empty)
            throw new ArgumentException("OriginalChatId cannot be empty.", nameof(originalChatId));
        return new ForwardReference(originalMessageId, originalChatId);
    }
}
