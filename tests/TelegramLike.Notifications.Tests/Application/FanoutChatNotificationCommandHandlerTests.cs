using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Application.Commands.FanoutChatNotification;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;
// Contracts and Domain both expose NotificationType now; these tests build the command
// with the domain enum, so alias it to keep the reference unambiguous.
using DomainNotificationType = TelegramLike.Notifications.Domain.ValueObjects.NotificationType;

namespace TelegramLike.Notifications.Tests.Application;

public class FanoutChatNotificationCommandHandlerTests
{
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private FanoutChatNotificationCommandHandler Handler => new(
        _notifications, _publish, NullLogger<FanoutChatNotificationCommandHandler>.Instance);

    private void StubAcceptAll() =>
        _notifications.AddManyIgnoringDuplicatesAsync(
                Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Notification>)call[0]).Count);

    [Fact]
    public async Task Fanout_CreatesOneNotificationPerRecipientExceptActor()
    {
        var chatId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();

        IReadOnlyCollection<Notification>? captured = null;
        _notifications.AddManyIgnoringDuplicatesAsync(
                Arg.Do<IReadOnlyCollection<Notification>>(n => captured = n), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Notification>)call[0]).Count);

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: chatId,
                TriggeredByUserId: actor,
                Type: DomainNotificationType.NewMessage,
                Recipients: new[] { member1, member2 },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Select(n => n.RecipientId).Should().BeEquivalentTo(new[] { member1, member2 });
    }

    [Fact]
    public async Task Fanout_ActorInRecipients_IsExcluded()
    {
        var actor = Guid.NewGuid();
        var other = Guid.NewGuid();

        IReadOnlyCollection<Notification>? captured = null;
        _notifications.AddManyIgnoringDuplicatesAsync(
                Arg.Do<IReadOnlyCollection<Notification>>(n => captured = n), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Notification>)call[0]).Count);

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                TriggeredByUserId: actor,
                Type: DomainNotificationType.NewMessage,
                Recipients: new[] { other, actor },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        captured!.Select(n => n.RecipientId).Should().ContainSingle().Which.Should().Be(other);
    }

    [Fact]
    public async Task Fanout_EmptyRecipients_DoesNotPersist()
    {
        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                TriggeredByUserId: Guid.NewGuid(),
                Type: DomainNotificationType.NewMessage,
                Recipients: [],
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        await _notifications.DidNotReceive().AddManyIgnoringDuplicatesAsync(
            Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fanout_NewMessageWithoutMessageId_Throws()
    {
        var act = () => Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                TriggeredByUserId: Guid.NewGuid(),
                Type: DomainNotificationType.NewMessage,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid(),
                MessageId: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Fanout_MemberJoinedWithoutMessageId_Succeeds()
    {
        StubAcceptAll();

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                TriggeredByUserId: Guid.NewGuid(),
                Type: DomainNotificationType.MemberJoined,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid()),
            CancellationToken.None);

        await _notifications.Received(1).AddManyIgnoringDuplicatesAsync(
            Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fanout_OnRedeliveryWithAllDuplicates_RepublishesUnreadCount()
    {
        // B11: inserted==0 (pure redelivery, or a fail-after-insert retry) must still
        // publish. Gating on inserted>0 loses the signal for good in the latter case,
        // and inserted==0 doesn't prove the badge already refreshed.
        _notifications.AddManyIgnoringDuplicatesAsync(
                Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                TriggeredByUserId: Guid.NewGuid(),
                Type: DomainNotificationType.NewMessage,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        await _publish.ReceivedWithAnyArgs(1).Publish<UnreadCountChangedIntegrationEvent>(default!);
    }
}
