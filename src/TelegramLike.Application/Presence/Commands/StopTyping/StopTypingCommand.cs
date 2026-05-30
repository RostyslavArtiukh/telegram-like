using MediatR;

namespace TelegramLike.Application.Presence.Commands.StopTyping;

public sealed record StopTypingCommand(Guid ChatId, Guid UserId) : IRequest;
