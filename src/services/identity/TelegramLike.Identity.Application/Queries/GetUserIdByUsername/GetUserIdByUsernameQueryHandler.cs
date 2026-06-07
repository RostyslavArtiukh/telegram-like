using MediatR;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Application.Queries.GetUserIdByUsername;

public sealed class GetUserIdByUsernameQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserIdByUsernameQuery, Guid?>
{
    public async Task<Guid?> Handle(GetUserIdByUsernameQuery request, CancellationToken cancellationToken)
    {
        Username username;
        try
        {
            username = Username.Create(request.Username);
        }
        catch (ArgumentException)
        {
            // Malformed username can never match a stored user — treat as "not found".
            return null;
        }

        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);
        return user?.Id;
    }
}
