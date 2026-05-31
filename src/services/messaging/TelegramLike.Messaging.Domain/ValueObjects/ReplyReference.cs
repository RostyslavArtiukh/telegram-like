namespace TelegramLike.Messaging.Domain.ValueObjects;

public sealed record ReplyReference(Guid ReplyToMessageId)
{
    public static ReplyReference To(Guid messageId)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("ReplyToMessageId cannot be empty.", nameof(messageId));
        return new ReplyReference(messageId);
    }
}
