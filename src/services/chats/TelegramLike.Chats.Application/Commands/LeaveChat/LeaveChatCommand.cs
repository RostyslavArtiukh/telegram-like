using MediatR;

namespace TelegramLike.Chats.Application.Commands.LeaveChat;

public sealed record LeaveChatCommand(Guid ChatId, Guid UserId) : IRequest;
