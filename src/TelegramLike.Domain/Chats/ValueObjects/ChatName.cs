namespace TelegramLike.Domain.Chats.ValueObjects;

public sealed record ChatName
{
    public const int MaxLength = 128;
    public string Value { get; }

    private ChatName(string value) => Value = value;

    public static ChatName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Chat name cannot be empty.");

        value = value.Trim();

        if (value.Length > MaxLength)
            throw new ArgumentException($"Chat name cannot exceed {MaxLength} characters.");

        return new ChatName(value);
    }

    public override string ToString() => Value;
}
