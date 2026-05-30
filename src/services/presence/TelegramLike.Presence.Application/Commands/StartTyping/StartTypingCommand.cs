using MediatR;

namespace TelegramLike.Presence.Application.Commands.StartTyping;

public sealed record StartTypingCommand(Guid ChatId, Guid UserId) : IRequest;
