using MediatR;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Application.Queries.GetBatchPresence;

public sealed class GetBatchPresenceQueryHandler(IPresenceCache presenceCache)
    : IRequestHandler<GetBatchPresenceQuery, IReadOnlyDictionary<Guid, bool>>
{
    public Task<IReadOnlyDictionary<Guid, bool>> Handle(
        GetBatchPresenceQuery request, CancellationToken cancellationToken)
        => presenceCache.AreOnlineAsync(request.UserIds, cancellationToken);
}
