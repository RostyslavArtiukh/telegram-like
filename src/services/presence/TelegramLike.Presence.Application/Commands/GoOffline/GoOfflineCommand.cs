using MediatR;

namespace TelegramLike.Presence.Application.Commands.GoOffline;

public sealed record GoOfflineCommand(Guid UserId) : IRequest;
