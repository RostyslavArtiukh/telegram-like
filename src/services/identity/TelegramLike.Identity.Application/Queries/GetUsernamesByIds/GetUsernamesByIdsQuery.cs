using MediatR;

namespace TelegramLike.Identity.Application.Queries.GetUsernamesByIds;

public sealed record GetUsernamesByIdsQuery(IReadOnlyCollection<Guid> UserIds)
    : IRequest<IReadOnlyDictionary<Guid, string>>;
