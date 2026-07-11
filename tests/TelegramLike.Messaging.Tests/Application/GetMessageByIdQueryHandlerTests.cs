using FluentAssertions;
using NSubstitute;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Application.Queries;
using TelegramLike.Messaging.Application.Queries.GetMessageById;

namespace TelegramLike.Messaging.Tests.Application;

public class GetMessageByIdQueryHandlerTests
{
    private readonly IMessageQueryService _queryService = Substitute.For<IMessageQueryService>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private GetMessageByIdQueryHandler Handler => new(_queryService, _membership);

    private static MessageDto MakeDto(Guid messageId, Guid chatId) => new(
        messageId, chatId, Guid.NewGuid(), "hi", [], null, null, null, [], false, null, null, null, DateTime.UtcNow);

    [Fact]
    public async Task GetMessageById_NonexistentMessage_ReturnsNull()
    {
        var requesterId = Guid.NewGuid();
        _queryService.GetMessageByIdAsync(Arg.Any<Guid>(), requesterId, Arg.Any<CancellationToken>())
            .Returns((MessageDto?)null);

        var result = await Handler.Handle(new GetMessageByIdQuery(Guid.NewGuid(), requesterId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMessageById_NonMemberOfKnownChat_ReturnsNullNot403()
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
    public async Task GetMessageById_MemberOfKnownChat_ReturnsMessage()
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
    public async Task GetMessageById_UnknownChat_FallsThroughAndReturnsMessage()
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
