using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Contracts.Notifications;
using TelegramLike.Notifications.Application.Commands.FanoutChatNotification;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Application.Tests;

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
    public async Task Creates_one_notification_per_recipient_except_actor()
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
                ActorId: actor,
                Type: NotificationType.NewMessage,
                Recipients: new[] { member1, member2 },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Select(n => n.RecipientId).Should().BeEquivalentTo(new[] { member1, member2 });
    }

    [Fact]
    public async Task Excludes_actor_even_if_present_in_recipients()
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
                ActorId: actor,
                Type: NotificationType.NewMessage,
                Recipients: new[] { other, actor },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        captured!.Select(n => n.RecipientId).Should().ContainSingle().Which.Should().Be(other);
    }

    [Fact]
    public async Task Empty_recipients_does_not_persist()
    {
        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                ActorId: Guid.NewGuid(),
                Type: NotificationType.NewMessage,
                Recipients: [],
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        await _notifications.DidNotReceive().AddManyIgnoringDuplicatesAsync(
            Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewMessage_without_message_id_throws()
    {
        var act = () => Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                ActorId: Guid.NewGuid(),
                Type: NotificationType.NewMessage,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid(),
                MessageId: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MemberJoined_without_message_id_succeeds()
    {
        StubAcceptAll();

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                ActorId: Guid.NewGuid(),
                Type: NotificationType.MemberJoined,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid()),
            CancellationToken.None);

        await _notifications.Received(1).AddManyIgnoringDuplicatesAsync(
            Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_unread_count_publish_when_all_were_duplicates()
    {
        _notifications.AddManyIgnoringDuplicatesAsync(
                Arg.Any<IReadOnlyCollection<Notification>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await Handler.Handle(
            new FanoutChatNotificationCommand(
                ChatId: Guid.NewGuid(),
                ActorId: Guid.NewGuid(),
                Type: NotificationType.NewMessage,
                Recipients: new[] { Guid.NewGuid() },
                SourceEventId: Guid.NewGuid(),
                MessageId: Guid.NewGuid()),
            CancellationToken.None);

        await _publish.DidNotReceiveWithAnyArgs().Publish<UnreadCountChangedIntegrationEvent>(default!);
    }
}
