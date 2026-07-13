using MassTransit;
using MediatR;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Commands.Heartbeat;

public sealed class HeartbeatCommandHandler(
    IUserPresenceRepository presenceRepository,
    IPresenceCache presenceCache,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<HeartbeatCommand>
{
    public async Task Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new DomainException("UserId cannot be empty.");

        // Redis is authoritative for "currently online". Check it BEFORE refreshing the
        // key so a reconnect after the heartbeat TTL expired is correctly seen as an
        // offline→online transition. (Mongo Status can be a stale "Online" that was
        // never reconciled when the key lapsed — gating on it would swallow the event.)
        var wasOnline = await presenceCache.IsOnlineAsync(request.UserId, cancellationToken);
        await presenceCache.TouchAsync(request.UserId, cancellationToken);
        if (wasOnline) return;

        var presence = await presenceRepository.GetByUserIdAsync(request.UserId, cancellationToken)
                       ?? UserPresence.CreateOffline(request.UserId);

        presence.GoOnline(DateTime.UtcNow);
        await presenceRepository.UpsertAsync(presence, cancellationToken);

        // Publish on every Redis offline→online edge, regardless of the (possibly
        // stale) durable Status, so consumers re-learn the user is back.
        await publishEndpoint.Publish(new UserCameOnlineIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserId: request.UserId), cancellationToken);
    }
}
