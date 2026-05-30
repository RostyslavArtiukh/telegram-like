using System.Text.Json;
using FluentAssertions;
using TelegramLike.Application.Chats.IntegrationEvents;
using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Domain.Chats.Aggregates;
using TelegramLike.Domain.Chats.ValueObjects;
using TelegramLike.Infrastructure.Outbox;
using TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;
using TelegramLike.Infrastructure.Tests.Fixtures;

namespace TelegramLike.Infrastructure.Tests.Persistence;

[Collection(IntegrationCollection.Name)]
public class ChatRepositoryIntegrationTests(IntegrationContainersFixture fx)
{
    private MongoOutboxStore NewStore() => new(fx.Database);

    private OutboxDomainEventDispatcher NewDispatcher() =>
        new(new IIntegrationEventMapper[]
        {
            new MemberJoinedEventMapper(),
            new MemberKickedEventMapper()
        }, NewStore());

    private ChatRepository NewRepo() => new(fx.MongoClient, fx.Database, NewDispatcher());

    [Fact]
    public async Task Add_then_GetById_round_trips_group_with_members()
    {
        var repo = NewRepo();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        chat.Join(member);

        await repo.AddAsync(chat);
        var loaded = await repo.GetByIdAsync(chat.Id);

        loaded.Should().NotBeNull().And.BeOfType<GroupChat>();
        loaded!.Members.Should().HaveCount(2);
        loaded.FindActiveMember(owner)!.Role.Should().Be(MemberRole.Owner);
        loaded.FindActiveMember(member)!.Role.Should().Be(MemberRole.Member);
    }

    [Fact]
    public async Task Update_persists_member_status_changes_in_separate_collection()
    {
        var repo = NewRepo();
        var owner = Guid.NewGuid();
        var kicked = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        chat.Join(kicked);
        await repo.AddAsync(chat);

        chat.Kick(kicked, owner);
        await repo.UpdateAsync(chat);

        var loaded = (GroupChat)(await repo.GetByIdAsync(chat.Id))!;
        loaded.ActiveMembers.Should().ContainSingle(m => m.UserId == owner);
        loaded.Members.Should().Contain(m => m.UserId == kicked && m.Status == MemberStatus.Kicked);
    }

    [Fact]
    public async Task FindDirectBetween_returns_existing_direct_chat()
    {
        var repo = NewRepo();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var direct = DirectChat.Create(alice, bob);
        await repo.AddAsync(direct);

        var found = await repo.FindDirectBetweenAsync(alice, bob);

        found.Should().NotBeNull();
        found!.Id.Should().Be(direct.Id);
    }

    [Fact]
    public async Task AddAsync_writes_MemberJoined_events_to_outbox()
    {
        var repo = NewRepo();
        var store = NewStore();
        var owner = Guid.NewGuid();
        var joiner = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        chat.Join(joiner);

        await repo.AddAsync(chat);

        var pending = await store.GetPendingAsync(batchSize: 100);
        var joinedEvents = pending
            .Where(p => p.EventType.Contains(nameof(MemberJoinedIntegrationEvent)))
            .Select(p => JsonSerializer.Deserialize<MemberJoinedIntegrationEvent>(p.Payload))
            .ToList();

        joinedEvents.Should().Contain(e => e!.ChatId == chat.Id && e.UserId == owner);
        joinedEvents.Should().Contain(e => e!.ChatId == chat.Id && e.UserId == joiner);
        chat.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_writes_MemberKicked_event_to_outbox()
    {
        var repo = NewRepo();
        var store = NewStore();
        var owner = Guid.NewGuid();
        var kicked = Guid.NewGuid();
        var chat = GroupChat.Create(ChatName.Create("Squad"), owner);
        chat.Join(kicked);
        await repo.AddAsync(chat);

        chat.Kick(kicked, owner);
        await repo.UpdateAsync(chat);

        var pending = await store.GetPendingAsync(batchSize: 100);
        var kickedPayload = pending
            .Where(p => p.EventType.Contains(nameof(MemberKickedIntegrationEvent)))
            .Select(p => JsonSerializer.Deserialize<MemberKickedIntegrationEvent>(p.Payload))
            .SingleOrDefault(e => e!.ChatId == chat.Id && e.UserId == kicked);

        kickedPayload.Should().NotBeNull();
        kickedPayload!.KickedBy.Should().Be(owner);
    }
}
