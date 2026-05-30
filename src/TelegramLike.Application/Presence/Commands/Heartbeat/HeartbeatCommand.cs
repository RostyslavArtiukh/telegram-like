using MediatR;

namespace TelegramLike.Application.Presence.Commands.Heartbeat;

public sealed record HeartbeatCommand(Guid UserId) : IRequest;
