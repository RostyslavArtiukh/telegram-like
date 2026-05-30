using MediatR;
using TelegramLike.Domain.Identity.Repositories;

namespace TelegramLike.Application.Identity.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return null;

        return new UserDto(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            user.DisplayName.Value,
            user.AvatarUrl,
            user.IsPremium,
            user.CreatedAt);
    }
}
