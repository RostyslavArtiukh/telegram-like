using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Tests;

public class RetractMessageCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private RetractMessageCommandHandler Handler =>
        new(_messageRepository, _membership, NullLogger<RetractMessageCommandHandler>.Instance);

    private static Message NewMessage(Guid chatId, Guid authorId)
        => Message.Send(Guid.NewGuid(), chatId, authorId, MessageContent.Create("hi"), [authorId]);

    [Fact]
    public async Task Author_can_retract_own_message()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.Handle(new RetractMessageCommand(message.Id, authorId, ActorIsModerator: false), CancellationToken.None);

        message.IsRetracted.Should().BeTrue();
        await _messageRepository.Received(1).UpdateAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Server_verified_moderator_can_retract_others_message()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, moderatorId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, moderatorId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new RetractMessageCommand(message.Id, moderatorId, ActorIsModerator: false), CancellationToken.None);

        message.IsRetracted.Should().BeTrue();
    }

    [Fact]
    public async Task Non_author_non_moderator_is_rejected_even_when_client_flag_claims_moderator()
    {
        // Regression: ActorIsModerator is a client-supplied flag and must be ignored —
        // authority comes from IChatMembershipReadModel.IsModeratorAsync only.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, attackerId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, attackerId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(
            new RetractMessageCommand(message.Id, attackerId, ActorIsModerator: true), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        message.IsRetracted.Should().BeFalse();
        await _messageRepository.DidNotReceive().UpdateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nonexistent_message_throws()
    {
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Handler.Handle(
            new RetractMessageCommand(Guid.NewGuid(), Guid.NewGuid(), ActorIsModerator: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Non_member_fails_open_but_still_needs_author_or_moderator_to_succeed()
    {
        // AddReaction/RemoveReaction fail-open on membership; retract also logs-only on
        // membership but still gates on author-or-moderator, so a non-member stranger
        // still can't retract someone else's message.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, strangerId, Arg.Any<CancellationToken>()).Returns(false);
        _membership.IsModeratorAsync(chatId, strangerId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(
            new RetractMessageCommand(message.Id, strangerId, ActorIsModerator: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
