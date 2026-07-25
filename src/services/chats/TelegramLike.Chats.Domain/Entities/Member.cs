using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Domain.Entities;

public sealed class Member : ObjectWithId
{
    public Guid UserId { get; private set; }
    public MemberRole Role { get; private set; }
    public MemberStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public Guid? KickedBy { get; private set; }
    public Guid? BannedBy { get; private set; }
    public string? BanReason { get; private set; }

    private Member() { }

    private Member(
        Guid id,
        Guid userId,
        MemberRole role,
        MemberStatus status,
        DateTime joinedAt,
        DateTime? leftAt,
        Guid? kickedBy,
        Guid? bannedBy,
        string? banReason)
        : base(id)
    {
        UserId = userId;
        Role = role;
        Status = status;
        JoinedAt = joinedAt;
        LeftAt = leftAt;
        KickedBy = kickedBy;
        BannedBy = bannedBy;
        BanReason = banReason;
    }

    public static Member Join(Guid userId, MemberRole role)
        => new(Guid.NewGuid(), userId, role, MemberStatus.Active, DateTime.UtcNow, null, null, null, null);

    public static Member FromStorage(
        Guid id,
        Guid userId,
        MemberRole role,
        MemberStatus status,
        DateTime joinedAt,
        DateTime? leftAt,
        Guid? kickedBy,
        Guid? bannedBy,
        string? banReason)
        => new(id, userId, role, status, joinedAt, leftAt, kickedBy, bannedBy, banReason);

    public bool IsActive => Status == MemberStatus.Active;

    internal void ChangeRole(MemberRole newRole)
    {
        Role = newRole;
    }

    internal void Leave()
    {
        Status = MemberStatus.Left;
        LeftAt = DateTime.UtcNow;
    }

    internal void Kick(Guid kickedBy)
    {
        Status = MemberStatus.Kicked;
        LeftAt = DateTime.UtcNow;
        KickedBy = kickedBy;
    }

    internal void Ban(Guid bannedBy, string? reason)
    {
        Status = MemberStatus.Banned;
        LeftAt = DateTime.UtcNow;
        BannedBy = bannedBy;
        BanReason = reason;
    }
}
