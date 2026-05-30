using MediatR;
using TelegramLike.Presence.Application.Abstractions;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Commands.Heartbeat;

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
