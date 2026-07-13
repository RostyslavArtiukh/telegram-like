using System.Text.RegularExpressions;

namespace TelegramLike.Identity.Domain.ValueObjects;

public sealed record Username
{
    public const int MaxLength = 32;
    private static readonly Regex AllowedChars = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    public string Value { get; }

    private Username(string value) => Value = value;

    public static Username Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Username cannot be empty.");

        value = value.Trim();

        if (value.Length < 3 || value.Length > MaxLength)
            throw new DomainException($"Username must be between 3 and {MaxLength} characters.");

        if (!AllowedChars.IsMatch(value))
            throw new DomainException("Username can only contain letters, digits, and underscores.");

        return new Username(value);
    }

    public override string ToString() => Value;
}
