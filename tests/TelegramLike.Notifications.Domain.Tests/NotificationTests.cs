using FluentAssertions;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Events;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Domain.Tests;

public class NotificationTests
{
    private static NotificationPayload AnyPayload() =>
        NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_starts_in_pending_status_and_raises_event()
    {
        var recipient = Guid.NewGuid();

        var n = Notification.Create(recipient, NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.RecipientId.Should().Be(recipient);
        n.Status.Should().Be(NotificationStatus.Pending);
        n.ReadAt.Should().BeNull();
        n.DomainEvents.OfType<NotificationCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_with_empty_recipient_throws()
    {
        var act = () => Notification.Create(Guid.Empty, NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_with_empty_source_event_id_throws()
    {
        var act = () => Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_persists_source_event_id_for_idempotency()
    {
        var sourceEventId = Guid.NewGuid();

        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), sourceEventId);

        n.SourceEventId.Should().Be(sourceEventId);
    }

    [Fact]
    public void MarkAsDelivered_transitions_pending_to_delivered()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.MarkAsDelivered();

        n.Status.Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public void MarkAsDelivered_after_read_is_noop()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        n.MarkAsRead();

        n.MarkAsDelivered();

        n.Status.Should().Be(NotificationStatus.Read);
    }

    [Fact]
    public void MarkAsRead_sets_read_at_and_raises_event()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.MarkAsRead();

        n.Status.Should().Be(NotificationStatus.Read);
        n.ReadAt.Should().NotBeNull();
        n.DomainEvents.OfType<NotificationReadEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkAsRead_twice_does_not_raise_extra_event()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        n.MarkAsRead();
        n.ClearDomainEvents();

        n.MarkAsRead();

        n.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Payload_for_member_joined_has_no_message_id()
    {
        var payload = NotificationPayload.ForMemberJoined(Guid.NewGuid(), Guid.NewGuid());

        payload.MessageId.Should().BeNull();
        payload.ActorId.Should().NotBeNull();
    }

    [Fact]
    public void Payload_for_new_message_requires_non_empty_message_id()
    {
        var act = () => NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }
}
