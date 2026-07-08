using TelegramLike.Messaging.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Application.Queries;
using TelegramLike.Messaging.Application.Queries.GetChatMessages;

namespace TelegramLike.Messaging.Application.Tests;

public class GetChatMessagesQueryHandlerTests
{
    private readonly IMessageQueryService _queryService = Substitute.For<IMessageQueryService>();
    private readonly IChatMembershipReadModel _membership = Substitute.For<IChatMembershipReadModel>();

    private GetChatMessagesQueryHandler Handler => new(_queryService, _membership);

    private static readonly MessagePageDto EmptyPage = new([], null);

    [Fact]
    public async Task Non_member_of_a_known_chat_is_rejected()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { Guid.NewGuid() }); // requester not in it

        var act = () => Handler.Handle(new GetChatMessagesQuery(chatId, requesterId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _queryService.DidNotReceive().GetChatMessagesAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Member_of_a_known_chat_is_allowed()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { requesterId });
        _queryService.GetChatMessagesAsync(chatId, requesterId, null, 50, Arg.Any<CancellationToken>())
            .Returns(EmptyPage);

        var result = await Handler.Handle(new GetChatMessagesQuery(chatId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(EmptyPage);
    }

    [Fact]
    public async Task Unknown_chat_falls_through_to_query_service()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _queryService.GetChatMessagesAsync(chatId, requesterId, null, 50, Arg.Any<CancellationToken>())
            .Returns(EmptyPage);

        var result = await Handler.Handle(new GetChatMessagesQuery(chatId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(EmptyPage);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(201, 50)]
    [InlineData(-5, 50)]
    [InlineData(100, 100)]
    public async Task PageSize_out_of_range_falls_back_to_default_50(int requested, int expected)
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _membership.GetActiveMemberIdsAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { requesterId });
        _queryService.GetChatMessagesAsync(chatId, requesterId, null, expected, Arg.Any<CancellationToken>())
            .Returns(EmptyPage);

        await Handler.Handle(new GetChatMessagesQuery(chatId, requesterId, PageSize: requested), CancellationToken.None);

        await _queryService.Received(1).GetChatMessagesAsync(
            chatId, requesterId, null, expected, Arg.Any<CancellationToken>());
    }
}
