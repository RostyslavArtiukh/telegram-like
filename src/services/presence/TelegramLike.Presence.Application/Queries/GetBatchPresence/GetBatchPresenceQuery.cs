using MediatR;

namespace TelegramLike.Presence.Application.Queries.GetBatchPresence;

public sealed record GetBatchPresenceQuery(IReadOnlyCollection<Guid> UserIds)
    : IRequest<IReadOnlyDictionary<Guid, bool>>;
