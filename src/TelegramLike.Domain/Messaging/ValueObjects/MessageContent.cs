namespace TelegramLike.Domain.Messaging.ValueObjects;

public sealed record MessageContent
{
    public const int MaxTextLength = 4096;

    public string? Text { get; }
    public IReadOnlyList<Attachment> Attachments { get; }

    private MessageContent(string? text, IReadOnlyList<Attachment> attachments)
    {
        Text = text;
        Attachments = attachments;
    }

    public static MessageContent Create(string? text, IReadOnlyList<Attachment>? attachments = null)
    {
        var normalizedText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var normalizedAttachments = (IReadOnlyList<Attachment>)(attachments?.ToList() ?? new List<Attachment>());

        if (normalizedText is null && normalizedAttachments.Count == 0)
            throw new ArgumentException("Message must contain text or at least one attachment.");

        if (normalizedText is not null && normalizedText.Length > MaxTextLength)
            throw new ArgumentException($"Message text cannot exceed {MaxTextLength} characters.");

        return new MessageContent(normalizedText, normalizedAttachments);
    }

    public bool IsEmpty => Text is null && Attachments.Count == 0;
}
