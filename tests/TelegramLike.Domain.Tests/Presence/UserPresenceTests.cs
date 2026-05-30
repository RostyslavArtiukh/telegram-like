using FluentAssertions;
using TelegramLike.Domain.Presence.Aggregates;
using TelegramLike.Domain.Presence.Events;
using TelegramLike.Domain.Presence.ValueObjects;

namespace TelegramLike.Domain.Tests.Presence;

public class UserPresenceTests
{
    [Fact]
    public void CreateOffline_starts_with_offline_status()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());

        p.Status.Should().Be(OnlineStatus.Offline);
        p.LastSeenAt.Should().BeNull();
        p.HideLastSeen.Should().BeFalse();
    }

    [Fact]
    public void CreateOffline_with_empty_id_throws()
    {
        var act = () => UserPresence.CreateOffline(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GoOnline_transitions_and_raises_event()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());

        p.GoOnline(DateTime.UtcNow);

        p.Status.Should().Be(OnlineStatus.Online);
        p.DomainEvents.OfType<UserCameOnlineEvent>().Should().ContainSingle();
    }

    [Fact]
    public void GoOnline_when_already_online_is_noop()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        p.ClearDomainEvents();

        p.GoOnline(DateTime.UtcNow);

        p.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void GoOffline_records_last_seen()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        var at = new DateTime(2026, 5, 23, 10, 0, 0, DateTimeKind.Utc);

        p.GoOffline(at);

        p.Status.Should().Be(OnlineStatus.Offline);
        p.LastSeenAt.Should().Be(at);
        p.DomainEvents.OfType<UserWentOfflineEvent>().Should().ContainSingle();
    }

    [Fact]
    public void GoOffline_hides_last_seen_when_HideLastSeen_is_true()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid(), hideLastSeen: true);
        p.GoOnline(DateTime.UtcNow);

        p.GoOffline(DateTime.UtcNow);

        p.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public void SetHideLastSeen_to_true_clears_existing_last_seen()
    {
        var p = UserPresence.CreateOffline(Guid.NewGuid());
        p.GoOnline(DateTime.UtcNow);
        p.GoOffline(DateTime.UtcNow);
        p.LastSeenAt.Should().NotBeNull();

        p.SetHideLastSeen(true);

        p.LastSeenAt.Should().BeNull();
    }
}
