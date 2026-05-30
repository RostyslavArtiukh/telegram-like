using MediatR;
using TelegramLike.Presence.Application.Abstractions;
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

        // Live override: Redis heartbeat key is authoritative for "currently online" — Mongo doc may lag.
        var isOnlineNow = await presenceCache.IsOnlineAsync(request.UserId, cancellationToken);
        var effectiveStatus = isOnlineNow ? OnlineStatus.Online : stored.Status;

        return stored with { Status = effectiveStatus };
    }
}
