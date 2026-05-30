using FluentAssertions;
using TelegramLike.Domain.Identity.Aggregates;
using TelegramLike.Domain.Identity.Events;

namespace TelegramLike.Domain.Tests.Identity;

public class UserTests
{
    private static User NewUser() =>
        User.Register("alice@example.com", "alice", "Alice", "hash");

    [Fact]
    public void Register_sets_default_state_and_raises_event()
    {
        var user = NewUser();

        user.Email.Value.Should().Be("alice@example.com");
        user.Status.Should().Be(AccountStatus.Active);
        user.IsPremium.Should().BeFalse();
        user.BlockedUserIds.Should().BeEmpty();
        user.DomainEvents.OfType<UserRegisteredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Block_adds_target_to_blocked_list_and_raises_event()
    {
        var user = NewUser();
        var target = Guid.NewGuid();

        user.Block(target);

        user.HasBlocked(target).Should().BeTrue();
        user.DomainEvents.OfType<UserBlockedEvent>()
            .Should().ContainSingle(e => e.BlockedUserId == target);
    }

    [Fact]
    public void Block_is_idempotent()
    {
        var user = NewUser();
        var target = Guid.NewGuid();

        user.Block(target);
        user.Block(target);

        user.BlockedUserIds.Count(id => id == target).Should().Be(1);
        user.DomainEvents.OfType<UserBlockedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void Block_self_throws()
    {
        var user = NewUser();
        var act = () => user.Block(user.Id);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Unblock_removes_target()
    {
        var user = NewUser();
        var target = Guid.NewGuid();
        user.Block(target);

        user.Unblock(target);

        user.HasBlocked(target).Should().BeFalse();
    }

    [Fact]
    public void CheckPremiumExpiry_disables_premium_when_expired()
    {
        var user = NewUser();
        user.ActivatePremium(DateTime.UtcNow.AddDays(-1));

        user.CheckPremiumExpiry();

        user.IsPremium.Should().BeFalse();
        user.PremiumExpiresAt.Should().BeNull();
    }

    [Fact]
    public void CheckPremiumExpiry_keeps_premium_when_not_expired()
    {
        var user = NewUser();
        user.ActivatePremium(DateTime.UtcNow.AddDays(7));

        user.CheckPremiumExpiry();

        user.IsPremium.Should().BeTrue();
    }
}
