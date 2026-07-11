using TelegramLike.Messaging.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Application;

public class RetractMessageCommandHandlerTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private RetractMessageCommandHandler Handler =>
        new(_messageRepository, _membership, NullLogger<RetractMessageCommandHandler>.Instance);

    private static Message NewMessage(Guid chatId, Guid authorId)
        => Message.Send(Guid.NewGuid(), chatId, authorId, MessageContent.Create("hi"), [authorId]);

    [Fact]
    public async Task Retract_ByAuthor_Succeeds()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, authorId, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.Handle(new RetractMessageCommand(message.Id, authorId, RetractedByModerator: false), CancellationToken.None);

        message.IsRetracted.Should().BeTrue();
        await _messageRepository.Received(1).UpdateAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retract_ByServerVerifiedModerator_SucceedsOnOthersMessage()
    {
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, moderatorId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, moderatorId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new RetractMessageCommand(message.Id, moderatorId, RetractedByModerator: false), CancellationToken.None);

        message.IsRetracted.Should().BeTrue();
    }

    [Fact]
    public async Task Retract_NonAuthorNonModerator_ThrowsEvenWhenClientFlagClaimsModerator()
    {
        // Regression: RetractedByModerator is a client-supplied flag and must be ignored —
        // authority comes from IChatMembershipReadModel.IsModeratorAsync only.
        var chatId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var message = NewMessage(chatId, authorId);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, attackerId, Arg.Any<CancellationToken>()).Returns(true);
        _membership.IsModeratorAsync(chatId, attackerId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(
            new RetractMessageCommand(message.Id, attackerId, RetractedByModerator: true), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        message.IsRetracted.Should().BeFalse();
        await _messageRepository.DidNotReceive().UpdateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retract_NonexistentMessage_Throws()
    {
        _messageRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Handler.Handle(
            new RetractMessageCommand(Guid.NewGuid(), Guid.NewGuid(), RetractedByModerator: false), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Retract_NonMemberFailsOpen_StillNeedsAuthorOrModerator()
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
            new RetractMessageCommand(message.Id, strangerId, RetractedByModerator: false), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
