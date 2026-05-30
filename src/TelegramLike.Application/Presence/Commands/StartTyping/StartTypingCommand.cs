using MediatR;

namespace TelegramLike.Application.Presence.Commands.StartTyping;

public sealed record StartTypingCommand(Guid ChatId, Guid UserId) : IRequest;
