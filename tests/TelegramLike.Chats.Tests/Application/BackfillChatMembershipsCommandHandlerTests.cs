using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Application.Commands.BackfillChatMemberships;
using TelegramLike.Contracts.Chats;
using TelegramLike.Shared.Application;

namespace TelegramLike.Chats.Tests.Application;

public class BackfillChatMembershipsCommandHandlerTests
{
    private readonly IChatMembershipBackfillReader _reader = Substitute.For<IChatMembershipBackfillReader>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private BackfillChatMembershipsCommandHandler Handler =>
        new(_reader, _publish, NullLogger<BackfillChatMembershipsCommandHandler>.Instance);

    private IReadOnlyList<ChatMembershipsSnapshotIntegrationEvent> Snapshots() =>
        _publish.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPublishEndpoint.Publish))
            .Select(c => c.GetArguments()[0])
            .OfType<ChatMembershipsSnapshotIntegrationEvent>()
            .ToList();

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
            new(chatA, "Group",
            [
                new ChatMembershipSnapshotMember(owner, "Owner", t0),
                new ChatMembershipSnapshotMember(member, "Member", t0.AddDays(1)),
            ]),
            new(chatB, "Broadcast", [new ChatMembershipSnapshotMember(solo, "Member", t0.AddDays(2))]),
        });

        var result = await Handler.Handle(new BackfillChatMembershipsCommand(), CancellationToken.None);

        result.ChatsPublished.Should().Be(2);
        result.MembersPublished.Should().Be(3);

        // Snapshots go out through the object/Type overload ([TL-124]): they are built as
        // IIntegrationEvent parts, and MassTransit routes on the type it is handed — the
        // interface would land on the wrong exchange.
        Snapshots().Should().SatisfyRespectively(
            // chatA snapshot carries both members with their own roles + JoinedAt.
            a =>
            {
                a.ChatId.Should().Be(chatA);
                a.Members.Should().HaveCount(2);
                a.Members.Should().Contain(m => m.UserId == owner && m.Role == "Owner" && m.JoinedAt == t0);
                a.Members.Should().Contain(m => m.UserId == member && m.Role == "Member" && m.JoinedAt == t0.AddDays(1));
            },
            b =>
            {
                b.ChatId.Should().Be(chatB);
                b.Members.Should().ContainSingle().Which.UserId.Should().Be(solo);
            });

        // Chat-type backfill ([TL-102]): one ChatCreated per chat carrying its type.
        await _publish.Received(1).Publish(
            Arg.Is<ChatCreatedIntegrationEvent>(e => e.ChatId == chatA && e.Type == "Group"),
            Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Is<ChatCreatedIntegrationEvent>(e => e.ChatId == chatB && e.Type == "Broadcast"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoChats_PublishesNothing_AndReturnsZero()
    {
        _reader.GetActiveMembershipsByChatAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ChatMembershipSnapshot>());

        var result = await Handler.Handle(new BackfillChatMembershipsCommand(), CancellationToken.None);

        result.Should().Be(new BackfillChatMembershipsResult(0, 0));
        Snapshots().Should().BeEmpty();
    }

    [Fact]
    public async Task ALargeChatsSnapshot_IsSplitIntoParts_ThatStillCoverEveryMember()
    {
        // A snapshot of a big chat was the single largest message this system produced — one
        // event carrying every active membership. Split, the parts must still add up exactly:
        // a member dropped here is one the read-models never materialize.
        var chatId = Guid.NewGuid();
        var members = Enumerable.Range(0, FanoutParts.MaxPerEvent + 7)
            .Select(i => new ChatMembershipSnapshotMember(Guid.NewGuid(), "Member", new DateTime(2026, 1, 1).AddMinutes(i)))
            .ToList();

        _reader.GetActiveMembershipsByChatAsync(Arg.Any<CancellationToken>())
            .Returns([new ChatMembershipSnapshot(chatId, "Group", members)]);

        var result = await Handler.Handle(new BackfillChatMembershipsCommand(), CancellationToken.None);

        // Counting is still per chat and per member, not per part.
        result.Should().Be(new BackfillChatMembershipsResult(1, members.Count));

        var parts = Snapshots();
        parts.Should().HaveCount(2);
        parts.Select(p => p.PartIndex).Should().Equal(0, 1);
        parts.Should().OnlyContain(p => p.ChatId == chatId && p.PartCount == 2);
        parts.SelectMany(p => p.Members.Select(m => m.UserId))
            .Should().Equal(members.Select(m => m.UserId));
    }
}
