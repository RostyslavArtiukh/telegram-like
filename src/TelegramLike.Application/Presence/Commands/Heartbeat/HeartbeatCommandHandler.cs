using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Presence.Aggregates;
using TelegramLike.Domain.Presence.Repositories;
using TelegramLike.Domain.Presence.ValueObjects;

namespace TelegramLike.Application.Presence.Commands.Heartbeat;

public sealed class HeartbeatCommandHandler(
    IUserPresenceRepository presenceRepository,
    IPresenceCache presenceCache)
    : IRequestHandler<HeartbeatCommand>
{
    public async Task Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(request));

        await presenceCache.TouchAsync(request.UserId, cancellationToken);

        var presence = await presenceRepository.GetByUserIdAsync(request.UserId, cancellationToken)
                       ?? UserPresence.CreateOffline(request.UserId);

        if (presence.Status == OnlineStatus.Online) return;

        presence.GoOnline(DateTime.UtcNow);
        await presenceRepository.UpsertAsync(presence, cancellationToken);
    }
}
