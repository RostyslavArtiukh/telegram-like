using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramLike.Messaging.Infrastructure.Storage;

[BsonIgnoreExtraElements]
internal sealed class ChatMembershipDocument
{
    // {chatId}:{userId} — composite key gives natural per-pair uniqueness
    // and idempotent upserts without a separate index.
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid ChatId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    // Soft membership state so the last-writer-wins guard keeps a timestamp even after
    // a leave/kick. Legacy docs written before these fields existed have neither — reads
    // treat a missing IsActive as active, and the conditional update treats a missing
    // LastEventAt as the epoch (so any real event wins).
    public bool IsActive { get; set; } = true;

    public DateTime LastEventAt { get; set; }

    // Chat role ("Owner"/"Admin"/"Member"/"Viewer"), materialized from MemberJoined +
    // MemberRoleChanged so retract can verify moderator authority server-side instead
    // of trusting a client flag. Legacy docs default to a non-moderator role.
    public string Role { get; set; } = "Member";

    public static string MakeId(Guid chatId, Guid userId) => $"{chatId:N}:{userId:N}";
}
