using MediatR;

namespace TelegramLike.Application.Presence.Commands.GoOffline;

public sealed record GoOfflineCommand(Guid UserId) : IRequest;
