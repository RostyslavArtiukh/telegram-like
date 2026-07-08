namespace TelegramLike.Messaging.Domain.ValueObjects;

public sealed record ReplyReference(Guid ReplyToMessageId)
{
    public static ReplyReference To(Guid messageId)
    {
        if (messageId == Guid.Empty)
            throw new DomainException("ReplyToMessageId cannot be empty.");
        return new ReplyReference(messageId);
    }
}
