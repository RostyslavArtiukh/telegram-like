using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Entities;

public sealed class Reaction : ObjectWithId
{
    public Guid UserId { get; private set; }
    public Emoji Emoji { get; private set; }
    public DateTime AddedAt { get; private set; }

    private Reaction() { }

    private Reaction(Guid id, Guid userId, Emoji emoji, DateTime addedAt) : base(id)
    {
        UserId = userId;
        Emoji = emoji;
        AddedAt = addedAt;
    }

    internal static Reaction Add(Guid userId, Emoji emoji)
        => new(Guid.NewGuid(), userId, emoji, DateTime.UtcNow);

    public static Reaction FromStorage(Guid id, Guid userId, Emoji emoji, DateTime addedAt)
        => new(id, userId, emoji, addedAt);
}
