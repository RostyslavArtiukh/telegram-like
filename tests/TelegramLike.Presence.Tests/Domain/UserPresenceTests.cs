using FluentAssertions;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Events;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Tests.Domain;

public class UserPresenceTests
{
    [Fact]
    public void CreateOffline_StartsWithOfflineStatus()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());

        p.Status.Should().Be(OnlineStatus.Offline);
        p.LastSeenAt.Should().BeNull();
        p.HideLastSeen.Should().BeFalse();
    }

    [Fact]
    public void CreateOffline_WithEmptyId_Throws()
    {
        var act = () => UserPresence.CreateOffline(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GoOnline_TransitionsAndRaisesEvent()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());

        p.GoOnline(DateTime.UtcNow);

        p.Status.Should().Be(OnlineStatus.Online);
        p.PendingEvents.OfType<UserCameOnlineEvent>().Should().ContainSingle();
    }

    [Fact]
    public void GoOnline_WhenAlreadyOnline_IsNoop()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        p.ClearPendingEvents();

        p.GoOnline(DateTime.UtcNow);

        p.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void GoOffline_RecordsLastSeen()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        var at = new DateTime(2026, 5, 23, 10, 0, 0, DateTimeKind.Utc);

        p.GoOffline(at);

        p.Status.Should().Be(OnlineStatus.Offline);
        p.LastSeenAt.Should().Be(at);
        p.PendingEvents.OfType<UserWentOfflineEvent>().Should().ContainSingle();
    }

    [Fact]
    public void GoOffline_WhenHideLastSeenIsTrue_HidesLastSeen()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid(), hideLastSeen: true);
        p.GoOnline(DateTime.UtcNow);

        p.GoOffline(DateTime.UtcNow);

        p.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public void SetHideLastSeen_ToTrue_ClearsExistingLastSeen()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        p.GoOffline(DateTime.UtcNow);
        p.LastSeenAt.Should().NotBeNull();

        p.SetHideLastSeen(true);

        p.LastSeenAt.Should().BeNull();
    }
}
