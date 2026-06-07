namespace TelegramLike.Identity.Domain.ValueObjects;

public sealed record DisplayName
{
    public const int MaxLength = 64;
    public string Value { get; }

    private DisplayName(string value) => Value = value;

    public static DisplayName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Display name cannot be empty.");

        value = value.Trim();

        if (value.Length > MaxLength)
            throw new ArgumentException($"Display name cannot exceed {MaxLength} characters.");

        return new DisplayName(value);
    }

    public override string ToString() => Value;
}
