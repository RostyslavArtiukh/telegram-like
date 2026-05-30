using FluentAssertions;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Domain.Tests.Chats;

public class BroadcastChannelTests
{
    private static ChatName Name => ChatName.Create("News");

    [Fact]
    public void Join_adds_member_as_Viewer()
    {
        var owner = Guid.NewGuid();
        var subscriber = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Name, owner);

        channel.Join(subscriber);

        channel.FindActiveMember(subscriber)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public void PromoteToAdmin_changes_role_to_Admin()
    {
        var owner = Guid.NewGuid();
        var subscriber = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Name, owner);
        channel.Join(subscriber);

        channel.PromoteToAdmin(subscriber, owner);

        channel.FindActiveMember(subscriber)!.Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public void PromoteToAdmin_by_non_owner_throws()
    {
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Name, owner);
        channel.Join(admin);
        channel.Join(viewer);
        channel.PromoteToAdmin(admin, owner);

        var act = () => channel.PromoteToAdmin(viewer, admin);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DemoteToViewer_brings_admin_back_to_viewer()
    {
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Name, owner);
        channel.Join(admin);
        channel.PromoteToAdmin(admin, owner);

        channel.DemoteToViewer(admin, owner);

        channel.FindActiveMember(admin)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public void DemoteToViewer_owner_throws()
    {
        var owner = Guid.NewGuid();
        var channel = BroadcastChannel.Create(Name, owner);

        var act = () => channel.DemoteToViewer(owner, owner);

        act.Should().Throw<InvalidOperationException>();
    }
}
