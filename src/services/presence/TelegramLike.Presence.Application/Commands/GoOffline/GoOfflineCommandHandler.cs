using MediatR;
using TelegramLike.Presence.Application.Abstractions;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Commands.GoOffline;

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
