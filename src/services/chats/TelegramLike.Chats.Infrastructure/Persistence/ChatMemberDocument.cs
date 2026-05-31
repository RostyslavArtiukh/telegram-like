using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Chats.Domain.Entities;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Persistence;

internal sealed class ChatMemberDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ChatId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public MemberRole Role { get; set; }

    [BsonRepresentation(BsonType.String)]
    public MemberStatus Status { get; set; }

    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? KickedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? BannedBy { get; set; }

    public string? BanReason { get; set; }

    public static ChatMemberDocument FromDomain(Member member, Guid chatId) => new()
    {
        Id = member.Id,
        ChatId = chatId,
        UserId = member.UserId,
        Role = member.Role,
        Status = member.Status,
        JoinedAt = member.JoinedAt,
        LeftAt = member.LeftAt,
        KickedBy = member.KickedBy,
        BannedBy = member.BannedBy,
        BanReason = member.BanReason
    };

    public Member ToDomain() => Member.Reconstitute(
        Id, UserId, Role, Status, JoinedAt, LeftAt, KickedBy, BannedBy, BanReason);
}
