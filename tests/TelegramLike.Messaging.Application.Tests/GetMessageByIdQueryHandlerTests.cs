using FluentAssertions;
using NSubstitute;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Application.Queries;
using TelegramLike.Messaging.Application.Queries.GetMessageById;

namespace TelegramLike.Messaging.Application.Tests;

public class GetMessageByIdQueryHandlerTests
{
    private readonly IMessageQueryService _queryService = Substitute.For<IMessageQueryService>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private GetMessageByIdQueryHandler Handler => new(_queryService, _membership);

    private static MessageDto MakeDto(Guid messageId, Guid chatId) => new(
        messageId, chatId, Guid.NewGuid(), "hi", [], null, null, null, [], false, null, null, null, DateTime.UtcNow);

    [Fact]
    public async Task Nonexistent_message_returns_null()
    {
        var requesterId = Guid.NewGuid();
        _queryService.GetMessageByIdAsync(Arg.Any<Guid>(), requesterId, Arg.Any<CancellationToken>())
            .Returns((MessageDto?)null);

        var result = await Handler.Handle(new GetMessageByIdQuery(Guid.NewGuid(), requesterId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Non_member_of_a_known_chat_gets_null_not_403()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dto = MakeDto(messageId, chatId);
        _queryService.GetMessageByIdAsync(messageId, requesterId, Arg.Any<CancellationToken>()).Returns(dto);
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { Guid.NewGuid() }); // requester not a member

        var result = await Handler.Handle(new GetMessageByIdQuery(messageId, requesterId), CancellationToken.None);

        result.Should().BeNull("a non-member should not be able to distinguish a hidden message from a nonexistent one");
    }

    [Fact]
    public async Task Member_of_a_known_chat_gets_the_message()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dto = MakeDto(messageId, chatId);
        _queryService.GetMessageByIdAsync(messageId, requesterId, Arg.Any<CancellationToken>()).Returns(dto);
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { requesterId });

        var result = await Handler.Handle(new GetMessageByIdQuery(messageId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task Unknown_chat_falls_through_and_returns_message()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dto = MakeDto(messageId, chatId);
        _queryService.GetMessageByIdAsync(messageId, requesterId, Arg.Any<CancellationToken>()).Returns(dto);
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        var result = await Handler.Handle(new GetMessageByIdQuery(messageId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }
}
