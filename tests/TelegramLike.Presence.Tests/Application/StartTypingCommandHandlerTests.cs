using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Application.Commands.StartTyping;

namespace TelegramLike.Presence.Tests.Application;

public class StartTypingCommandHandlerTests
{
    private readonly ITypingIndicatorService _typing = Substitute.For<ITypingIndicatorService>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private StartTypingCommandHandler Handler => new(_typing, _membership, _publish);

    [Fact]
    public async Task StartTyping_ActiveMember_PublishesEvent()
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
    public async Task StartTyping_NonMember_ThrowsForbidden()
    {
        // Fail-closed ([TL-101]): the read model is backfilled, so a non-member is refused —
        // no typing indicator is set and no typing event is published.
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _membership.IsActiveMemberAsync(chatId, userId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(new StartTypingCommand(chatId, userId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _typing.DidNotReceive().StartTypingAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _publish.DidNotReceive().Publish(
            Arg.Any<UserTypingIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
