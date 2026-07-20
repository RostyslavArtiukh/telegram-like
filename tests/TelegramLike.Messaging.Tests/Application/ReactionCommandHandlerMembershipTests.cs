using FluentAssertions;
using NSubstitute;
using TelegramLike.Messaging.Application.Commands.AddReaction;
using TelegramLike.Messaging.Application.Commands.RemoveReaction;
using TelegramLike.Messaging.Application.Observability;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Application;

/// <summary>
/// AddReaction/RemoveReaction enforce membership fail-closed ([TL-101]): once the read model is
/// backfilled, a non-member is authoritative and refused with a 403; a member proceeds normally.
/// </summary>
public class ReactionCommandHandlerMembershipTests
{
    private readonly IMessageRepository _messageRepository = Substitute.For<IMessageRepository>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private readonly MessagingMetrics _metrics = new();

    private AddReactionCommandHandler AddHandler => new(_messageRepository, _membership, _metrics);
    private RemoveReactionCommandHandler RemoveHandler => new(_messageRepository, _membership);

    private static Message NewMessage(Guid chatId, Guid authorId)
        => Message.Send(Guid.NewGuid(), chatId, authorId, MessageContent.Create("hi"), [authorId]);

    [Fact]
    public async Task AddReaction_NonMember_ThrowsForbidden()
    {
        var chatId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var message = NewMessage(chatId, Guid.NewGuid());
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, nonMemberId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => AddHandler.Handle(
            new AddReactionCommand(message.Id, nonMemberId, Emoji.Like, UserIsPremium: false), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        message.Reactions.Should().BeEmpty();
        await _messageRepository.DidNotReceive().UpdateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddReaction_Member_Succeeds()
    {
        var chatId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var message = NewMessage(chatId, Guid.NewGuid());
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, memberId, Arg.Any<CancellationToken>()).Returns(true);

        await AddHandler.Handle(
            new AddReactionCommand(message.Id, memberId, Emoji.Like, UserIsPremium: false), CancellationToken.None);

        message.Reactions.Should().ContainSingle(r => r.UserId == memberId);
        await _messageRepository.Received(1).UpdateAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveReaction_NonMember_ThrowsForbidden()
    {
        var chatId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var message = NewMessage(chatId, Guid.NewGuid());
        message.AddReaction(nonMemberId, Emoji.Like, isPremium: false);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, nonMemberId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => RemoveHandler.Handle(
            new RemoveReactionCommand(message.Id, nonMemberId, Emoji.Like), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        message.Reactions.Should().ContainSingle(r => r.UserId == nonMemberId); // unchanged
        await _messageRepository.DidNotReceive().UpdateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveReaction_Member_Succeeds()
    {
        var chatId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var message = NewMessage(chatId, Guid.NewGuid());
        message.AddReaction(memberId, Emoji.Like, isPremium: false);
        _messageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _membership.IsActiveMemberAsync(chatId, memberId, Arg.Any<CancellationToken>()).Returns(true);

        await RemoveHandler.Handle(
            new RemoveReactionCommand(message.Id, memberId, Emoji.Like), CancellationToken.None);

        message.Reactions.Should().BeEmpty();
        await _messageRepository.Received(1).UpdateAsync(message, Arg.Any<CancellationToken>());
    }
}
