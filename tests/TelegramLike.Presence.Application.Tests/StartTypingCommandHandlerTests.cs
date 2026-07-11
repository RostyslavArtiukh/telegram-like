using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Application.Commands.StartTyping;

namespace TelegramLike.Presence.Application.Tests;

public class StartTypingCommandHandlerTests
{
    private readonly ITypingIndicatorService _typing = Substitute.For<ITypingIndicatorService>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private StartTypingCommandHandler Handler => new(_typing, _membership, _publish, NullLogger<StartTypingCommandHandler>.Instance);

    [Fact]
    public async Task Active_member_starts_typing_and_publishes_event()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _membership.IsActiveMemberAsync(chatId, userId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new StartTypingCommand(chatId, userId), CancellationToken.None);

        await _typing.Received(1).StartTypingAsync(chatId, userId, Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Is<UserTypingIntegrationEvent>(e => e.ChatId == chatId && e.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_is_allowed_through_fail_open_until_read_model_backfilled()
    {
        // Until the read model is fully populated for legacy chats we keep
        // letting unknown pairs through (the handler logs a warning). Lock this
        // in so a future "tighten to fail-closed" change has to consciously
        // update the test, not slip past unnoticed.
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _membership.IsActiveMemberAsync(chatId, userId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(new StartTypingCommand(chatId, userId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _typing.Received(1).StartTypingAsync(chatId, userId, Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Any<UserTypingIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }
}
