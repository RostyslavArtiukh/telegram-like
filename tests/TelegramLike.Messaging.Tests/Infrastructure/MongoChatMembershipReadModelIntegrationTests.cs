using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class MongoChatMembershipReadModelIntegrationTests(MongoFixture fx)
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private MongoChatMembershipReadModel NewReadModel() => new(fx.Database);

    [Fact]
    public async Task UpsertActive_ThenIsActiveMember_ReturnsTrue()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, "Member", T0);

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveMemberIds_ReturnsOnlyActiveMembersOfChat()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var leftUser = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, a, "Owner", T0);
        await sut.UpsertActiveAsync(chatId, b, "Member", T0);
        await sut.UpsertActiveAsync(chatId, leftUser, "Member", T0);
        await sut.DeactivateAsync(chatId, leftUser, T0.AddSeconds(1));

        var ids = await sut.GetActiveMemberIdsAsync(chatId);

        ids.Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public async Task StaleJoinRedeliveredAfterLeave_DoesNotResurrectMembership()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatId, userId, "Member", T0);
        await sut.DeactivateAsync(chatId, userId, T0.AddSeconds(5));
        await sut.UpsertActiveAsync(chatId, userId, "Member", T0); // stale redelivery

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeFalse(
            "a join older than the last leave must not resurrect the membership");
    }

    [Fact]
    public async Task StaleLeaveRedeliveredAfterRejoin_DoesNotRemoveMember()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await sut.DeactivateAsync(chatId, userId, T0);
        await sut.UpsertActiveAsync(chatId, userId, "Member", T0.AddSeconds(5));
        await sut.DeactivateAsync(chatId, userId, T0); // stale redelivery

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue(
            "a leave older than the last rejoin must not remove the active member");
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Admin", true)]
    [InlineData("Member", false)]
    [InlineData("Viewer", false)]
    public async Task IsModerator_ReflectsMaterializedRole(string role, bool expectedModerator)
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.UpsertActiveAsync(chatId, userId, role, T0);

        (await sut.IsModeratorAsync(chatId, userId)).Should().Be(expectedModerator);
    }

    [Fact]
    public async Task IsModerator_InactiveMemberWithOwnerRole_ReturnsFalse()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.UpsertActiveAsync(chatId, userId, "Owner", T0);
        await sut.DeactivateAsync(chatId, userId, T0.AddSeconds(1));

        (await sut.IsModeratorAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task SetRole_PromotesMemberToModerator()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.UpsertActiveAsync(chatId, userId, "Member", T0);

        await sut.SetRoleAsync(chatId, userId, "Admin", T0.AddSeconds(1));

        (await sut.IsModeratorAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task SetRole_LastWriterWins_IgnoresStaleDemotion()
    {
        var sut = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.UpsertActiveAsync(chatId, userId, "Member", T0);

        await sut.SetRoleAsync(chatId, userId, "Admin", T0.AddSeconds(10));
        await sut.SetRoleAsync(chatId, userId, "Member", T0.AddSeconds(1)); // stale, older

        (await sut.IsModeratorAsync(chatId, userId)).Should().BeTrue(
            "a stale role-change event must not override a newer one");
    }

    [Fact]
    public async Task LegacyDocumentWithoutIsActiveOrRole_ReadsAsActiveNonModerator()
    {
        // Docs written before these fields existed have neither. IsActiveMemberAsync
        // treats a missing IsActive as active; IsModeratorAsync treats a missing Role
        // as a non-moderator ("Member" default), per ChatMembershipDocument's comments.
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var collection = fx.Database.GetCollection<BsonDocument>("chat_memberships");
        await collection.InsertOneAsync(new BsonDocument
        {
            { "_id", ChatMembershipDocument.MakeId(chatId, userId) },
            { "ChatId", chatId.ToString() },
            { "UserId", userId.ToString() },
            // Deliberately no IsActive, no Role, no LastEventAt.
        });

        var sut = NewReadModel();

        (await sut.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
        (await sut.IsModeratorAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task DifferentChats_AreIsolated()
    {
        var sut = NewReadModel();
        var userId = Guid.NewGuid();
        var chatA = Guid.NewGuid();
        var chatB = Guid.NewGuid();

        await sut.UpsertActiveAsync(chatA, userId, "Owner", T0);

        (await sut.IsActiveMemberAsync(chatA, userId)).Should().BeTrue();
        (await sut.IsActiveMemberAsync(chatB, userId)).Should().BeFalse();
    }
}
