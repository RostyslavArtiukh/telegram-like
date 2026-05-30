using TelegramLike.Domain.Common;
using TelegramLike.Domain.Presence.Events;
using TelegramLike.Domain.Presence.ValueObjects;

namespace TelegramLike.Domain.Presence.Aggregates;

public sealed class UserPresence : AggregateRoot
{
    public OnlineStatus Status { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public bool HideLastSeen { get; private set; }

    private UserPresence() { }

    private UserPresence(Guid userId, OnlineStatus status, DateTime? lastSeenAt, bool hideLastSeen)
        : base(userId)
    {
        Status = status;
        LastSeenAt = lastSeenAt;
        HideLastSeen = hideLastSeen;
    }

    public static UserPresence CreateOffline(Guid userId, bool hideLastSeen = false)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        return new UserPresence(userId, OnlineStatus.Offline, lastSeenAt: null, hideLastSeen);
    }

    public static UserPresence Reconstitute(Guid userId, OnlineStatus status, DateTime? lastSeenAt, bool hideLastSeen)
        => new(userId, status, lastSeenAt, hideLastSeen);

    public void GoOnline(DateTime at)
    {
        if (Status == OnlineStatus.Online) return;

        Status = OnlineStatus.Online;
        RaiseDomainEvent(new UserCameOnlineEvent(Id, at));
    }

    public void GoOffline(DateTime at)
    {
        if (Status == OnlineStatus.Offline) return;

        Status = OnlineStatus.Offline;
        LastSeenAt = HideLastSeen ? null : at;
        RaiseDomainEvent(new UserWentOfflineEvent(Id, LastSeenAt));
    }

    public void SetHideLastSeen(bool hide)
    {
        HideLastSeen = hide;
        if (hide) LastSeenAt = null;
    }
}
