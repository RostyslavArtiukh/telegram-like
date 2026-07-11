using MediatR;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Queries.GetUserPresence;

public sealed class GetUserPresenceQueryHandler(
    IUserPresenceQueryService queryService,
    IPresenceCache presenceCache)
    : IRequestHandler<GetUserPresenceQuery, UserPresenceDto?>
{
    public async Task<UserPresenceDto?> Handle(GetUserPresenceQuery request, CancellationToken cancellationToken)
    {
        var stored = await queryService.GetByUserIdAsync(request.UserId, cancellationToken);
        if (stored is null) return null;

        // Redis heartbeat key is authoritative for "currently online". Mongo Status is
        // never reconciled to Offline when the key lapses (browser close), so a stale
        // "Online" there must NOT leak through — a Redis miss means offline. This keeps
        // this endpoint consistent with the batch endpoint (which reads Redis directly).
        var isOnlineNow = await presenceCache.IsOnlineAsync(request.UserId, cancellationToken);
        var effectiveStatus = isOnlineNow ? OnlineStatus.Online : OnlineStatus.Offline;

        return stored with { Status = effectiveStatus };
    }
}
