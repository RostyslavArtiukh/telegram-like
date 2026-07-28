using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Infrastructure.Storage;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

namespace TelegramLike.Chats.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class ChatRepositoryIntegrationTests(MongoFixture fx)
{
    private ChatRepository NewRepository(IOutgoingEventsWriter? writer = null)
        => new(fx.MongoClient, fx.Database, writer ?? new RecordingOutgoingEventsWriter());

    // ── Add + GetById: the aggregate is split over two collections ─────────

    [Fact]
    public async Task Add_ThenGetById_RoundTripsGroupChatWithMembers()
    {
        var repo = NewRepository();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("dnd party"), ownerId);
        chat.Join(memberId);

        await repo.AddAsync(chat);
        var loaded = await repo.GetByIdAsync(chat.Id);

        loaded.Should().BeOfType<GroupChat>();
        loaded!.Name!.Value.Should().Be("dnd party");
        loaded.CreatedBy.Should().Be(ownerId);
        loaded.Members.Should().HaveCount(2);
        loaded.FindActiveMember(ownerId)!.Role.Should().Be(MemberRole.Owner);
        loaded.FindActiveMember(memberId)!.Role.Should().Be(MemberRole.Member);
    }

    [Fact]
    public async Task Add_ThenGetById_RoundTripsDirectChat()
    {
        var repo = NewRepository();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var chat = DirectChat.Create(Guid.NewGuid(), userA, userB);

        await repo.AddAsync(chat);
        var loaded = await repo.GetByIdAsync(chat.Id);

        loaded.Should().BeOfType<DirectChat>();
        loaded!.FindActiveMember(userA).Should().NotBeNull();
        loaded.FindActiveMember(userB).Should().NotBeNull();
    }

    [Fact]
    public async Task Add_ThenGetById_RoundTripsBroadcastChannelWithViewer()
    {
        var repo = NewRepository();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var chat = BroadcastChannel.Create(Guid.NewGuid(), ChatName.Create("news"), ownerId);
        chat.Join(viewerId);

        await repo.AddAsync(chat);
        var loaded = await repo.GetByIdAsync(chat.Id);

        loaded.Should().BeOfType<BroadcastChannel>();
        loaded!.FindActiveMember(viewerId)!.Role.Should().Be(MemberRole.Viewer);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        var repo = NewRepository();

        var loaded = await repo.GetByIdAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    // ── Idempotent Add: a retried create must not duplicate anything ───────

    [Fact]
    public async Task Add_WithDuplicateChatId_IsSwallowedIdempotently()
    {
        var repo = NewRepository();
        var chatId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await repo.AddAsync(GroupChat.Create(chatId, ChatName.Create("g"), ownerId));

        // A retried create reuses the same client-supplied id.
        var retry = GroupChat.Create(chatId, ChatName.Create("g"), ownerId);
        var act = () => repo.AddAsync(retry);

        await act.Should().NotThrowAsync();
        retry.PendingEvents.Should().BeEmpty("the swallowed retry must not leave events queued for re-dispatch");

        var loaded = await repo.GetByIdAsync(chatId);
        loaded!.Members.Should().HaveCount(1, "the aborted transaction must not re-insert member rows");
    }

    [Fact]
    public async Task Add_DrainsPendingEventsToOutboxWriter_AndClearsThem()
    {
        var writer = new RecordingOutgoingEventsWriter();
        var repo = NewRepository(writer);
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), Guid.NewGuid());
        var pendingBefore = chat.PendingEvents.Count;

        await repo.AddAsync(chat);

        pendingBefore.Should().BeGreaterThan(0);
        writer.Written.Should().HaveCount(pendingBefore);
        chat.PendingEvents.Should().BeEmpty();
    }

    // ── Update: member upserts keep history rows ───────────────────────────

    [Fact]
    public async Task Update_PersistsNewlyJoinedMember()
    {
        var repo = NewRepository();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), Guid.NewGuid());
        await repo.AddAsync(chat);

        var joinerId = Guid.NewGuid();
        var reloaded = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        reloaded.Join(joinerId);
        await repo.UpdateAsync(reloaded);

        var final = await repo.GetByIdAsync(chat.Id);
        final!.FindActiveMember(joinerId)!.Role.Should().Be(MemberRole.Member);
    }

    [Fact]
    public async Task Update_KeepsKickedMemberRow_WithKickedStatus()
    {
        var repo = NewRepository();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        chat.Join(memberId);
        await repo.AddAsync(chat);

        var reloaded = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        reloaded.Kick(memberId, ownerId);
        await repo.UpdateAsync(reloaded);

        var final = await repo.GetByIdAsync(chat.Id);
        final!.FindActiveMember(memberId).Should().BeNull();
        var kickedRow = final.Members.Single(m => m.UserId == memberId);
        kickedRow.Status.Should().Be(MemberStatus.Kicked);
        kickedRow.KickedBy.Should().Be(ownerId);
    }

    // ── Rejoin must not accumulate ghost rows in chat_members ─────────────

    [Fact]
    public async Task Update_AfterLeaveRejoinCycles_KeepsExactlyOneRowPerMember()
    {
        // Update upserts by member row id and never deletes, so a rejoin that minted a
        // fresh row grew chat_members by one document per cycle — permanently.
        var repo = NewRepository();
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        chat.Join(userId);
        await repo.AddAsync(chat);

        for (var i = 0; i < 3; i++)
        {
            var leaving = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
            leaving.Leave(userId);
            await repo.UpdateAsync(leaving);

            var rejoining = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
            rejoining.Join(userId);
            await repo.UpdateAsync(rejoining);
        }

        var final = await repo.GetByIdAsync(chat.Id);
        final!.Members.Where(m => m.UserId == userId).Should().ContainSingle();
        final.ActiveMembers.Should().HaveCount(2, "the owner plus the one rejoined member");
    }

    [Fact]
    public async Task Update_BanAfterLeaveRejoinHistory_LeavesNoActiveRowBehind()
    {
        // The consequence that made the ghost rows more than cosmetic: Ban resolves its
        // target with FindAnyMember, which could pick the stale Left row and mark *that*
        // one Banned while the member's live row stayed Active.
        var repo = NewRepository();
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), ownerId);
        chat.Join(userId);
        await repo.AddAsync(chat);

        var leaving = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        leaving.Leave(userId);
        await repo.UpdateAsync(leaving);

        var rejoining = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        rejoining.Join(userId);
        await repo.UpdateAsync(rejoining);

        var banning = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        banning.Ban(userId, ownerId, "spam");
        await repo.UpdateAsync(banning);

        var final = await repo.GetByIdAsync(chat.Id);
        final!.FindActiveMember(userId).Should().BeNull("a banned member must not remain active through a duplicate row");
        final.Members.Where(m => m.UserId == userId).Should().ContainSingle()
            .Which.Status.Should().Be(MemberStatus.Banned);
    }

    // ── The transaction is all-or-nothing across both collections + outbox ─

    [Fact]
    public async Task Update_WhenOutboxWriteFails_RollsBackChatAndMemberWrites()
    {
        var goodRepo = NewRepository();
        var ownerId = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("before"), ownerId);
        await goodRepo.AddAsync(chat);

        var joinerId = Guid.NewGuid();
        var reloaded = (GroupChat)(await goodRepo.GetByIdAsync(chat.Id))!;
        reloaded.Rename(ChatName.Create("after"), ownerId);
        reloaded.Join(joinerId);

        var failingRepo = NewRepository(new ThrowingOutgoingEventsWriter());
        var act = () => failingRepo.UpdateAsync(reloaded);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var final = await goodRepo.GetByIdAsync(chat.Id);
        final!.Name!.Value.Should().Be("before", "the rename must roll back with the failed transaction");
        final.FindActiveMember(joinerId).Should().BeNull("the member insert must roll back with the failed transaction");
    }

    // ── FindDirectBetween ──────────────────────────────────────────────────

    [Fact]
    public async Task FindDirectBetween_FindsPairInEitherOrder()
    {
        var repo = NewRepository();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var direct = DirectChat.Create(Guid.NewGuid(), userA, userB);
        await repo.AddAsync(direct);

        (await repo.FindDirectBetweenAsync(userA, userB))!.Id.Should().Be(direct.Id);
        (await repo.FindDirectBetweenAsync(userB, userA))!.Id.Should().Be(direct.Id);
    }

    [Fact]
    public async Task FindDirectBetween_IgnoresSharedGroupChat()
    {
        var repo = NewRepository();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var group = GroupChat.Create(Guid.NewGuid(), ChatName.Create("shared"), userA);
        group.Join(userB);
        await repo.AddAsync(group);

        var found = await repo.FindDirectBetweenAsync(userA, userB);

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindDirectBetween_WithDirectsOnlyToOthers_ReturnsNull()
    {
        var repo = NewRepository();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        await repo.AddAsync(DirectChat.Create(Guid.NewGuid(), userA, userC));
        await repo.AddAsync(DirectChat.Create(Guid.NewGuid(), userB, userC));

        var found = await repo.FindDirectBetweenAsync(userA, userB);

        found.Should().BeNull();
    }
}
