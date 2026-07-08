namespace TelegramLike.Messaging.Domain.ValueObjects;

public sealed record ForwardReference(Guid OriginalMessageId, Guid OriginalChatId)
{
    public static ForwardReference From(Guid originalMessageId, Guid originalChatId)
    {
        if (originalMessageId == Guid.Empty)
            throw new DomainException("OriginalMessageId cannot be empty.");
        if (originalChatId == Guid.Empty)
            throw new DomainException("OriginalChatId cannot be empty.");
        return new ForwardReference(originalMessageId, originalChatId);
    }
}
