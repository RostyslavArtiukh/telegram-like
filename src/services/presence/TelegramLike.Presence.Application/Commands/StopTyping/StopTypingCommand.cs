using MediatR;

namespace TelegramLike.Presence.Application.Commands.StopTyping;

public sealed record StopTypingCommand(Guid ChatId, Guid UserId) : IRequest;
