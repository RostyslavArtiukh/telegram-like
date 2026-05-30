using FluentAssertions;
using TelegramLike.Application.Chats.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Tests.Chats;

public class MemberEventMappersTests
{
    [Fact]
    public void MemberJoined_mapper_copies_identifiers_timestamps_and_recipients()
    {
        var recipients = new[] { Guid.NewGuid() };
        var domainEvent = new MemberJoinedEvent(
            ChatId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Role: MemberRole.Member,
            Recipients: recipients);

        var result = (MemberJoinedIntegrationEvent)new MemberJoinedEventMapper().Map(domainEvent);

        result.EventId.Should().Be(domainEvent.EventId);
        result.OccurredAt.Should().Be(domainEvent.OccurredAt);
        result.ChatId.Should().Be(domainEvent.ChatId);
        result.UserId.Should().Be(domainEvent.UserId);
        result.Recipients.Should().BeEquivalentTo(recipients);
    }

    [Fact]
    public void MemberJoined_mapper_DomainEventType_is_correct()
    {
        new MemberJoinedEventMapper().DomainEventType.Should().Be(typeof(MemberJoinedEvent));
    }

    [Fact]
    public void MemberKicked_mapper_copies_identifiers_actor_and_recipients()
    {
        var recipients = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var domainEvent = new MemberKickedEvent(
            ChatId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            KickedBy: Guid.NewGuid(),
            Recipients: recipients);

        var result = (MemberKickedIntegrationEvent)new MemberKickedEventMapper().Map(domainEvent);

        result.EventId.Should().Be(domainEvent.EventId);
        result.OccurredAt.Should().Be(domainEvent.OccurredAt);
        result.ChatId.Should().Be(domainEvent.ChatId);
        result.UserId.Should().Be(domainEvent.UserId);
        result.KickedBy.Should().Be(domainEvent.KickedBy);
        result.Recipients.Should().BeEquivalentTo(recipients);
    }

    [Fact]
    public void MemberKicked_mapper_DomainEventType_is_correct()
    {
        new MemberKickedEventMapper().DomainEventType.Should().Be(typeof(MemberKickedEvent));
    }
}
