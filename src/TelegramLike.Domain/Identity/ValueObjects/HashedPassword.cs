namespace TelegramLike.Domain.Identity.ValueObjects;

public sealed record HashedPassword
{
    public string Hash { get; }

    private HashedPassword(string hash) => Hash = hash;

    public static HashedPassword FromHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Password hash cannot be empty.");
        return new HashedPassword(hash);
    }

    public override string ToString() => Hash;
}
