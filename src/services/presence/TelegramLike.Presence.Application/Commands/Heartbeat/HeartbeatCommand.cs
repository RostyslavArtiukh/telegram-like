using MediatR;

namespace TelegramLike.Presence.Application.Commands.Heartbeat;

public sealed record HeartbeatCommand(Guid UserId) : IRequest;
