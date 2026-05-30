using MediatR;

namespace TelegramLike.Application.Chats.Commands.LeaveChat;

public sealed record LeaveChatCommand(Guid ChatId, Guid UserId) : IRequest;
