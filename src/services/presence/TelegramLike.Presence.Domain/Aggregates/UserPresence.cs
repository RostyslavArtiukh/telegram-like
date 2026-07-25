using TelegramLike.Shared.Domain;
using TelegramLike.Presence.Domain.Events;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Domain.Aggregates;

public sealed class UserPresence : ObjectWithEvents
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
            throw new DomainException("UserId cannot be empty.");

        return new UserPresence(userId, OnlineStatus.Offline, lastSeenAt: null, hideLastSeen);
    }

    public static UserPresence FromStorage(Guid userId, OnlineStatus status, DateTime? lastSeenAt, bool hideLastSeen)
        => new(userId, status, lastSeenAt, hideLastSeen);

    public void GoOnline(DateTime at)
    {
        if (Status == OnlineStatus.Online) return;

        Status = OnlineStatus.Online;
        RecordEvent(new UserCameOnlineEvent(Id, at));
    }

    public void GoOffline(DateTime at)
    {
        if (Status == OnlineStatus.Offline) return;

        Status = OnlineStatus.Offline;
        LastSeenAt = HideLastSeen ? null : at;
        RecordEvent(new UserWentOfflineEvent(Id, LastSeenAt));
    }

    public void SetHideLastSeen(bool hide)
    {
        HideLastSeen = hide;
        if (hide) LastSeenAt = null;
    }
}
