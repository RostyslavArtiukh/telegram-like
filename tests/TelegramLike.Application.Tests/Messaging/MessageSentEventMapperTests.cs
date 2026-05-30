using FluentAssertions;
using TelegramLike.Application.Messaging.IntegrationEvents;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Messaging.Events;

namespace TelegramLike.Application.Tests.Messaging;

public class MessageSentEventMapperTests
{
    [Fact]
    public void Map_copies_identifiers_timestamps_and_recipients()
    {
        var recipients = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var domainEvent = new MessageSentEvent(
            MessageId: Guid.NewGuid(),
            ChatId: Guid.NewGuid(),
            AuthorId: Guid.NewGuid(),
            ReplyToMessageId: null,
            ForwardOriginalMessageId: null,
            Recipients: recipients);

        var result = (MessageSentIntegrationEvent)new MessageSentEventMapper().Map(domainEvent);

        result.EventId.Should().Be(domainEvent.EventId);
        result.OccurredAt.Should().Be(domainEvent.OccurredAt);
        result.MessageId.Should().Be(domainEvent.MessageId);
        result.ChatId.Should().Be(domainEvent.ChatId);
        result.AuthorId.Should().Be(domainEvent.AuthorId);
        result.Recipients.Should().BeEquivalentTo(recipients);
    }

    [Fact]
    public void DomainEventType_is_MessageSentEvent()
    {
        new MessageSentEventMapper().DomainEventType.Should().Be(typeof(MessageSentEvent));
    }
}
