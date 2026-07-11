using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Queries;
using TelegramLike.Chats.Application.Queries.GetChatById;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Tests.Application;

public class GetChatByIdQueryHandlerTests
{
    private readonly IChatQueryService _queryService = Substitute.For<IChatQueryService>();

    private GetChatByIdQueryHandler Handler => new(_queryService);

    private static ChatDetailsDto MakeDto(Guid chatId) => new(
        chatId, ChatType.Group, "name", Guid.NewGuid(), DateTime.UtcNow, false, []);

    [Fact]
    public async Task GetChatById_ForNonMember_ReturnsNullNotDetails()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _queryService.IsActiveMemberAsync(chatId, requesterId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Handler.Handle(new GetChatByIdQuery(chatId, requesterId), CancellationToken.None);

        result.Should().BeNull();
        await _queryService.DidNotReceive().GetChatByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChatById_ForActiveMember_ReturnsDetails()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var dto = MakeDto(chatId);
        _queryService.IsActiveMemberAsync(chatId, requesterId, Arg.Any<CancellationToken>()).Returns(true);
        _queryService.GetChatByIdAsync(chatId, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await Handler.Handle(new GetChatByIdQuery(chatId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }
}
