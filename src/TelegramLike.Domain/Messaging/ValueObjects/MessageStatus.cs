namespace TelegramLike.Domain.Messaging.ValueObjects;

public sealed record MessageStatus
{
    public bool IsRetracted { get; }
    public DateTime? RetractedAt { get; }
    public Guid? RetractedBy { get; }

    private MessageStatus(bool isRetracted, DateTime? retractedAt, Guid? retractedBy)
    {
        IsRetracted = isRetracted;
        RetractedAt = retractedAt;
        RetractedBy = retractedBy;
    }

    public static MessageStatus Active() => new(false, null, null);

    public static MessageStatus Retracted(Guid retractedBy, DateTime retractedAt)
        => new(true, retractedAt, retractedBy);
}
