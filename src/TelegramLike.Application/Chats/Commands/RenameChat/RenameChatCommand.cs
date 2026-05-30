using MediatR;

namespace TelegramLike.Application.Chats.Commands.RenameChat;

public sealed record RenameChatCommand(Guid ChatId, string NewName, Guid ActorUserId) : IRequest;
