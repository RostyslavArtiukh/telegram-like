using MediatR;

namespace TelegramLike.Chats.Application.Commands.DeleteChat;

public sealed record DeleteChatCommand(Guid ChatId, Guid DeletedByUserId) : IRequest;
