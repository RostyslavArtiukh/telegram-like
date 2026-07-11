using FluentAssertions;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Infrastructure.Storage;
using TelegramLike.Chats.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Chats.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class ChatQueryServiceIntegrationTests(MongoFixture fx)
{
    private ChatQueryService NewQueryService() => new(fx.Database);
    private ChatRepository NewRepository() => new(fx.MongoClient, fx.Database, new RecordingOutgoingEventsWriter());

    [Fact]
    public async Task GetMyChats_ReturnsRoleAndActiveMemberCountPerChat()
    {
        var repo = NewRepository();
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var kicked = Guid.NewGuid();

        var group = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), me);
        group.Join(friend);
        group.Join(kicked);
        group.Kick(kicked, me);
        await repo.AddAsync(group);

        var summaries = await NewQueryService().GetMyChatsAsync(me);

        var summary = summaries.Single(s => s.ChatId == group.Id);
        summary.MyRole.Should().Be(MemberRole.Owner);
        summary.ActiveMemberCount.Should().Be(2, "the kicked member must not count");
        summary.Name.Should().Be("g");
    }

    [Fact]
    public async Task GetMyChats_ExcludesLeftAndDeletedChats()
    {
        var repo = NewRepository();
        var me = Guid.NewGuid();
        var owner = Guid.NewGuid();

        var leftChat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("left"), owner);
        leftChat.Join(me);
        leftChat.Leave(me);
        await repo.AddAsync(leftChat);

        var deletedChat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("gone"), me);
        deletedChat.Delete(me);
        await repo.AddAsync(deletedChat);

        var keptChat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("kept"), me);
        await repo.AddAsync(keptChat);

        var summaries = await NewQueryService().GetMyChatsAsync(me);

        summaries.Select(s => s.ChatId).Should().Equal(keptChat.Id);
    }

    [Fact]
    public async Task GetMyChats_WithNoMemberships_ReturnsEmpty()
    {
        var summaries = await NewQueryService().GetMyChatsAsync(Guid.NewGuid());

        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChatById_ReturnsDetailsWithAllMemberRows()
    {
        var repo = NewRepository();
        var owner = Guid.NewGuid();
        var kicked = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), owner);
        chat.Join(kicked);
        chat.Kick(kicked, owner);
        await repo.AddAsync(chat);

        var details = await NewQueryService().GetChatByIdAsync(chat.Id);

        details!.ChatId.Should().Be(chat.Id);
        details.Type.Should().Be(ChatType.Group);
        details.CreatedBy.Should().Be(owner);
        details.IsDeleted.Should().BeFalse();
        // The roster keeps history rows: the kicked member is present with its status.
        details.Members.Should().HaveCount(2);
        details.Members.Single(m => m.UserId == kicked).Status.Should().Be(MemberStatus.Kicked);
    }

    [Fact]
    public async Task GetChatById_UnknownChat_ReturnsNull()
    {
        var details = await NewQueryService().GetChatByIdAsync(Guid.NewGuid());

        details.Should().BeNull();
    }

    [Fact]
    public async Task IsActiveMember_TrueForActive_FalseForKickedOrUnknown()
    {
        var repo = NewRepository();
        var owner = Guid.NewGuid();
        var kicked = Guid.NewGuid();
        var chat = GroupChat.Create(Guid.NewGuid(), ChatName.Create("g"), owner);
        chat.Join(kicked);
        chat.Kick(kicked, owner);
        await repo.AddAsync(chat);

        var queries = NewQueryService();

        (await queries.IsActiveMemberAsync(chat.Id, owner)).Should().BeTrue();
        (await queries.IsActiveMemberAsync(chat.Id, kicked)).Should().BeFalse();
        (await queries.IsActiveMemberAsync(chat.Id, Guid.NewGuid())).Should().BeFalse();
    }
}
