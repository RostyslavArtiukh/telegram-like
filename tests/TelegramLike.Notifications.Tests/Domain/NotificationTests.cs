using FluentAssertions;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Events;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Tests.Domain;

public class NotificationTests
{
    private static NotificationPayload AnyPayload() =>
        NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_StartsInPendingStatusAndRaisesEvent()
    {
        var recipient = Guid.NewGuid();

        var n = Notification.Create(recipient, NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.RecipientId.Should().Be(recipient);
        n.Status.Should().Be(NotificationStatus.Pending);
        n.ReadAt.Should().BeNull();
        n.PendingEvents.OfType<NotificationCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_WithEmptyRecipient_Throws()
    {
        var act = () => Notification.Create(Guid.Empty, NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptySourceEventId_Throws()
    {
        var act = () => Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PersistsSourceEventIdForIdempotency()
    {
        var sourceEventId = Guid.NewGuid();

        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), sourceEventId);

        n.SourceEventId.Should().Be(sourceEventId);
    }

    [Fact]
    public void MarkAsDelivered_TransitionsPendingToDelivered()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.MarkAsDelivered();

        n.Status.Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public void MarkAsDelivered_AfterRead_IsNoop()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        n.MarkAsRead();

        n.MarkAsDelivered();

        n.Status.Should().Be(NotificationStatus.Read);
    }

    [Fact]
    public void MarkAsRead_SetsReadAtAndRaisesEvent()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());

        n.MarkAsRead();

        n.Status.Should().Be(NotificationStatus.Read);
        n.ReadAt.Should().NotBeNull();
        n.PendingEvents.OfType<NotificationReadEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkAsRead_Twice_DoesNotRaiseExtraEvent()
    {
        var n = Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, AnyPayload(), Guid.NewGuid());
        n.MarkAsRead();
        n.ClearPendingEvents();

        n.MarkAsRead();

        n.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void Payload_ForMemberJoined_HasNoMessageId()
    {
        var payload = NotificationPayload.ForMemberJoined(Guid.NewGuid(), Guid.NewGuid());

        payload.MessageId.Should().BeNull();
        payload.TriggeredByUserId.Should().NotBeNull();
    }

    [Fact]
    public void Payload_ForNewMessage_RequiresNonEmptyMessageId()
    {
        var act = () => NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }
}
