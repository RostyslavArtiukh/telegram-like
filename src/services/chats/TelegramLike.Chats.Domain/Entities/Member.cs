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

    /// <summary>
    /// Revives this row for a member who had left or been kicked, as a fresh join.
    /// </summary>
    /// <remarks>
    /// A rejoin must reuse the existing row rather than mint a second one. The row's
    /// <see cref="ObjectWithId.Id"/> is what <c>chat_members</c> is upserted by, so a
    /// replacement row leaves the old Left/Kicked one orphaned in the collection forever —
    /// and every lookup that resolves a user through <c>FindAnyMember</c> (notably
    /// <c>Ban</c>) then picks whichever row Mongo happens to return first, which could be
    /// the ghost while the live row stayed active.
    /// </remarks>
    internal void Rejoin(MemberRole role)
    {
        Role = role;
        Status = MemberStatus.Active;
        JoinedAt = DateTime.UtcNow;
        LeftAt = null;
        KickedBy = null;
        BannedBy = null;
        BanReason = null;
    }

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
