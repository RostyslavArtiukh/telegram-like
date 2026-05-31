namespace TelegramLike.Messaging.Domain.ValueObjects;

public sealed record Attachment
{
    public AttachmentType Type { get; }
    public string Url { get; }
    public long SizeBytes { get; }
    public string? FileName { get; }

    private Attachment(AttachmentType type, string url, long sizeBytes, string? fileName)
    {
        Type = type;
        Url = url;
        SizeBytes = sizeBytes;
        FileName = fileName;
    }

    public static Attachment Create(AttachmentType type, string url, long sizeBytes, string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Attachment url cannot be empty.", nameof(url));

        if (sizeBytes <= 0)
            throw new ArgumentException("Attachment size must be positive.", nameof(sizeBytes));

        return new Attachment(type, url.Trim(), sizeBytes, string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim());
    }
}
