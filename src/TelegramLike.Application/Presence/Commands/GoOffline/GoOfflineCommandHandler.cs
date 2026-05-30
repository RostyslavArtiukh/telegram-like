using MediatR;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Presence.Repositories;
using TelegramLike.Domain.Presence.ValueObjects;

namespace TelegramLike.Application.Presence.Commands.GoOffline;

public sealed class GoOfflineCommandHandler(
    IUserPresenceRepository presenceRepository,
    IPresenceCache presenceCache)
    : IRequestHandler<GoOfflineCommand>
{
    public async Task Handle(GoOfflineCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(request));

        await presenceCache.ClearAsync(request.UserId, cancellationToken);

        var presence = await presenceRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (presence is null || presence.Status == OnlineStatus.Offline) return;

        presence.GoOffline(DateTime.UtcNow);
        await presenceRepository.UpsertAsync(presence, cancellationToken);
    }
}
