using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Entities;

public sealed class Reaction : Entity
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

    public static Reaction Reconstitute(Guid id, Guid userId, Emoji emoji, DateTime addedAt)
        => new(id, userId, emoji, addedAt);
}
