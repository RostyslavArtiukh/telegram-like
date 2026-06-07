using MediatR;
using TelegramLike.Identity.Domain.Repositories;

namespace TelegramLike.Identity.Application.Queries.GetUsernamesByIds;

public sealed class GetUsernamesByIdsQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUsernamesByIdsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> Handle(
        GetUsernamesByIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0) return new Dictionary<Guid, string>();

        var users = await userRepository.GetByIdsAsync(request.UserIds, cancellationToken);
        return users.ToDictionary(u => u.Id, u => u.Username.Value);
    }
}
