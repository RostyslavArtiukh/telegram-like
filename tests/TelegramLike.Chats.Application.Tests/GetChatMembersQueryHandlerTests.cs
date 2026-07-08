using TelegramLike.Chats.Domain;
using FluentAssertions;
using NSubstitute;
using TelegramLike.Chats.Application.Common.Interfaces;
using TelegramLike.Chats.Application.Queries;
using TelegramLike.Chats.Application.Queries.GetChatMembers;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Tests;

public class GetChatMembersQueryHandlerTests
{
    private readonly IChatQueryService _queryService = Substitute.For<IChatQueryService>();

    private GetChatMembersQueryHandler Handler => new(_queryService);

    [Fact]
    public async Task Non_member_is_rejected_with_403()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        _queryService.IsActiveMemberAsync(chatId, requesterId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => Handler.Handle(new GetChatMembersQuery(chatId, requesterId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _queryService.DidNotReceive().GetChatMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Active_member_gets_the_roster()
    {
        var chatId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var members = new List<ChatMemberDto>
        {
            new(requesterId, MemberRole.Owner, MemberStatus.Active, DateTime.UtcNow, null)
        };
        _queryService.IsActiveMemberAsync(chatId, requesterId, Arg.Any<CancellationToken>()).Returns(true);
        _queryService.GetChatMembersAsync(chatId, Arg.Any<CancellationToken>()).Returns(members);

        var result = await Handler.Handle(new GetChatMembersQuery(chatId, requesterId), CancellationToken.None);

        result.Should().BeSameAs(members);
    }
}
