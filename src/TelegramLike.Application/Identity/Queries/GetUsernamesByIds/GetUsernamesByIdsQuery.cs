using MediatR;

namespace TelegramLike.Application.Identity.Queries.GetUsernamesByIds;

public sealed record GetUsernamesByIdsQuery(IReadOnlyCollection<Guid> UserIds)
    : IRequest<IReadOnlyDictionary<Guid, string>>;
