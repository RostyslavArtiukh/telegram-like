using MediatR;

namespace TelegramLike.Chats.Application.Commands.RenameChat;

public sealed record RenameChatCommand(Guid ChatId, string NewName, Guid RenamedByUserId) : IRequest;
