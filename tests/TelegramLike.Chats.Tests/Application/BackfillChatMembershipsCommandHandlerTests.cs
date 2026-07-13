using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Application.Commands.BackfillChatMemberships;
using TelegramLike.Contracts.Chats;

namespace TelegramLike.Chats.Tests.Application;

public class BackfillChatMembershipsCommandHandlerTests
{
    private readonly IChatMembershipBackfillReader _reader = Substitute.For<IChatMembershipBackfillReader>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private BackfillChatMembershipsCommandHandler Handler =>
        new(_reader, _publish, NullLogger<BackfillChatMembershipsCommandHandler>.Instance);

    [Fact]
    public async Task Publishes_OneSnapshotPerChat_WithMappedMembers_AndReturnsCounts()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var solo = Guid.NewGuid();

        _reader.GetActiveMembershipsByChatAsync(Arg.Any<CancellationToken>()).Returns(new List<ChatMembershipSnapshot>
        {
            new(chatA,
            [
                new ChatMembershipSnapshotMember(owner, "Owner", t0),
                new ChatMembershipSnapshotMember(member, "Member", t0.AddDays(1)),
            ]),
            new(chatB, [new ChatMembershipSnapshotMember(solo, "Member", t0.AddDays(2))]),
        });

        var result = await Handler.Handle(new BackfillChatMembershipsCommand(), CancellationToken.None);

        result.ChatsPublished.Should().Be(2);
        result.MembersPublished.Should().Be(3);

        // chatA snapshot carries both members with their own roles + JoinedAt.
        await _publish.Received(1).Publish(
            Arg.Is<ChatMembershipsSnapshotIntegrationEvent>(e =>
                e.ChatId == chatA
                && e.Members.Count == 2
                && e.Members.Any(m => m.UserId == owner && m.Role == "Owner" && m.JoinedAt == t0)
                && e.Members.Any(m => m.UserId == member && m.Role == "Member" && m.JoinedAt == t0.AddDays(1))),
            Arg.Any<CancellationToken>());

        await _publish.Received(1).Publish(
            Arg.Is<ChatMembershipsSnapshotIntegrationEvent>(e =>
                e.ChatId == chatB && e.Members.Count == 1 && e.Members[0].UserId == solo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoChats_PublishesNothing_AndReturnsZero()
    {
        _reader.GetActiveMembershipsByChatAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ChatMembershipSnapshot>());

        var result = await Handler.Handle(new BackfillChatMembershipsCommand(), CancellationToken.None);

        result.Should().Be(new BackfillChatMembershipsResult(0, 0));
        await _publish.DidNotReceive().Publish(
            Arg.Any<ChatMembershipsSnapshotIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
