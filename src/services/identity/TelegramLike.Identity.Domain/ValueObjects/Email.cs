namespace TelegramLike.Identity.Domain.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty.");

        value = value.Trim().ToLowerInvariant();

        if (!value.Contains('@') || !value.Contains('.'))
            throw new ArgumentException($"'{value}' is not a valid email.");

        return new Email(value);
    }

    public override string ToString() => Value;
}
