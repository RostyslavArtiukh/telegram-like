using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Infrastructure.Storage;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Chats.Tests.Infrastructure;

/// <summary>
/// The unique (ChatId, UserId) index is the database-side backstop behind Member.Rejoin:
/// the aggregate revives a member's row instead of inserting a second one, and the index
/// guarantees nothing else can reintroduce a duplicate. Because it has to be applied to
/// databases the old rejoin path already polluted, the initializer prunes first.
/// </summary>
[Collection(MongoCollection.Name)]
public class ChatIndexTests(MongoFixture fx)
{
    // A private database per test: indexes and pruning are per-collection state.
    private IMongoDatabase NewDatabase()
        => fx.MongoClient.GetDatabase($"tl_chats_idx_test_{Guid.NewGuid():N}");

    private static IMongoCollection<ChatMemberDocument> Members(IMongoDatabase database)
        => database.GetCollection<ChatMemberDocument>("chat_members");

    private static ChatMemberDocument Row(
        Guid chatId, Guid userId, MemberStatus status, DateTime joinedAt, MemberRole role = MemberRole.Member) => new()
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            UserId = userId,
            Role = role,
            Status = status,
            JoinedAt = joinedAt
        };

    [Fact]
    public async Task EnsureIndexes_CreatesTheUniqueMembershipIndex()
    {
        var database = NewDatabase();

        await ChatIndexes.EnsureIndexesAsync(database);

        var all = await (await Members(database).Indexes.ListAsync()).ToListAsync();
        var index = all.FirstOrDefault(i => i["name"].AsString == "uniq_chat_member");
        index.Should().NotBeNull();
        index!["key"].AsBsonDocument.Names.Should().Equal("ChatId", "UserId");
        index["unique"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureIndexes_RejectsASecondRowForTheSameMember()
    {
        var database = NewDatabase();
        await ChatIndexes.EnsureIndexesAsync(database);
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await Members(database).InsertOneAsync(Row(chatId, userId, MemberStatus.Left, DateTime.UtcNow));

        var insertDuplicate = async () =>
            await Members(database).InsertOneAsync(Row(chatId, userId, MemberStatus.Active, DateTime.UtcNow));

        await insertDuplicate.Should().ThrowAsync<MongoWriteException>()
            .Where(ex => ex.WriteError.Category == ServerErrorCategory.DuplicateKey);
    }

    [Fact]
    public async Task EnsureIndexes_WhenRunTwice_DoesNotThrow()
    {
        var database = NewDatabase();
        await ChatIndexes.EnsureIndexesAsync(database);

        // Every service restart re-runs this hosted service.
        var rerun = async () => await ChatIndexes.EnsureIndexesAsync(database);

        await rerun.Should().NotThrowAsync();
    }

    // ── Pruning the rows the old rejoin path left behind ───────────────────

    [Fact]
    public async Task EnsureIndexes_PrunesLegacyDuplicates_KeepingTheActiveRow()
    {
        var database = NewDatabase();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var live = Row(chatId, userId, MemberStatus.Active, now);
        await Members(database).InsertManyAsync(
        [
            Row(chatId, userId, MemberStatus.Left, now.AddHours(-2)),
            Row(chatId, userId, MemberStatus.Kicked, now.AddHours(-1)),
            live
        ]);

        var pruned = await ChatIndexes.EnsureIndexesAsync(database);

        pruned.Should().Be(2);
        var remaining = await Members(database).Find(m => m.UserId == userId).ToListAsync();
        remaining.Should().ContainSingle().Which.Id.Should().Be(live.Id);
    }

    [Fact]
    public async Task EnsureIndexes_PrunesLegacyDuplicates_KeepsTheBanOverTheActiveRow()
    {
        // The exact state the bug produced: Ban marked a stale row while the live row
        // stayed Active. Collapsing to the Active row would silently readmit a moderated
        // user, so the ban wins.
        var database = NewDatabase();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var banned = Row(chatId, userId, MemberStatus.Banned, now.AddHours(-2));
        await Members(database).InsertManyAsync([banned, Row(chatId, userId, MemberStatus.Active, now)]);

        await ChatIndexes.EnsureIndexesAsync(database);

        var remaining = await Members(database).Find(m => m.UserId == userId).ToListAsync();
        remaining.Should().ContainSingle().Which.Status.Should().Be(MemberStatus.Banned);
        remaining.Single().Id.Should().Be(banned.Id);
    }

    [Fact]
    public async Task EnsureIndexes_PrunesLegacyDuplicates_KeepsNewestWhenAllAreInactive()
    {
        var database = NewDatabase();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var newest = Row(chatId, userId, MemberStatus.Left, now);
        await Members(database).InsertManyAsync([Row(chatId, userId, MemberStatus.Left, now.AddDays(-3)), newest]);

        await ChatIndexes.EnsureIndexesAsync(database);

        var remaining = await Members(database).Find(m => m.UserId == userId).ToListAsync();
        remaining.Should().ContainSingle().Which.Id.Should().Be(newest.Id);
    }

    [Fact]
    public async Task EnsureIndexes_LeavesDistinctMembersAndChatsAlone()
    {
        var database = NewDatabase();
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await Members(database).InsertManyAsync(
        [
            Row(chatA, userA, MemberStatus.Active, now),
            Row(chatA, userB, MemberStatus.Active, now),
            // Same user in a different chat is a legitimate second row.
            Row(chatB, userA, MemberStatus.Active, now)
        ]);

        var pruned = await ChatIndexes.EnsureIndexesAsync(database);

        pruned.Should().Be(0);
        (await Members(database).CountDocumentsAsync(FilterDefinition<ChatMemberDocument>.Empty)).Should().Be(3);
    }

    [Fact]
    public async Task EnsureIndexes_OnAnEmptyCollection_DoesNotThrow()
    {
        var database = NewDatabase();

        var act = async () => await ChatIndexes.EnsureIndexesAsync(database);

        await act.Should().NotThrowAsync();
    }
}
