using MediatR;

namespace TelegramLike.Application.Chats.Commands.CreateGroupChat;

public sealed record CreateGroupChatCommand(Guid OwnerUserId, string Name) : IRequest<Guid>;
