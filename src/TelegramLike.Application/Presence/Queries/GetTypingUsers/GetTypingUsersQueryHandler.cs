using MediatR;
using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Application.Presence.Queries.GetTypingUsers;

public sealed class GetTypingUsersQueryHandler(ITypingIndicatorService typingService)
    : IRequestHandler<GetTypingUsersQuery, TypingUsersDto>
{
    public async Task<TypingUsersDto> Handle(GetTypingUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await typingService.GetTypingUserIdsAsync(request.ChatId, cancellationToken);
        return new TypingUsersDto(request.ChatId, users);
    }
}
